using GeoProfiles.Model;
using NetTopologySuite.Geometries;

namespace GeoProfiles.Infrastructure.Services;

public record ProfilePoint(double Distance, double Elevation);

public class TerrainProfileData
{
    public Guid Id { get; init; }
    public decimal LengthM { get; init; }
    public IReadOnlyList<ProfilePoint> Points { get; init; } = null!;
}

public interface ITerrainProfileService
{
    Task<TerrainProfileData> BuildProfileAsync(
        Point start,
        Point end,
        Guid projectId,
        double samplingMeters = 10.0,
        CancellationToken ct = default);
}

public class TerrainProfileService(
    GeoProfilesContext db,
    IElevationProvider elevProv,
    IIsolineGeneratorService isolSvc)
    : ITerrainProfileService
{
    public async Task<TerrainProfileData> BuildProfileAsync(
        Point start,
        Point end,
        Guid projectId,
        double samplingMeters = 10.0,
        CancellationToken ct = default)
    {
        var project = await db.Projects.FindAsync([projectId], ct)
                      ?? throw new KeyNotFoundException("Project not found");

        if (!project.Bbox.Contains(start) || !project.Bbox.Contains(end))
        {
            // TODO доделать догенерацию изолиний
            // var expanded = project.Bbox.Buffer(samplingMeters * 5);
            // await _isolSvc.GenerateMore(projectId, expanded);
            //throw new NotImplementedException();
        }

        var totalDist = start.Distance(end);
        var n0 = (int) Math.Ceiling(totalDist / samplingMeters);
        var rawPts = new List<Point>(n0 + 1);
        for (var i = 0; i <= n0; i++)
        {
            var t = (double) i / n0;
            rawPts.Add(new Point(
                    start.X + (end.X - start.X) * t,
                    start.Y + (end.Y - start.Y) * t)
                {SRID = 4326});
        }

        var x0 = new double[rawPts.Count];
        var y0 = new double[rawPts.Count];
        double cum = 0;
        var prev = rawPts[0];
        for (var i = 0; i < rawPts.Count; i++)
        {
            var pt = rawPts[i];
            var h = await elevProv.GetElevationAsync(pt, ct);
            if (i > 0) cum += prev.Distance(pt);
            x0[i] = cum;
            y0[i] = (double) h;
            prev = pt;
        }

        var interpolator = CubicSpline.CreatePchip(x0, y0);

        const int n = 800;

        var xs = new double[n];
        var ys = new double[n];
        for (var i = 0; i < n; i++)
        {
            var x = totalDist * i / (n - 1);
            xs[i] = x;
            ys[i] = interpolator.Interpolate(x);
        }

        var points = xs
            .Zip(ys, (dist, elev) =>
                new ProfilePoint(dist, elev))
            .ToList();


        await db.SaveChangesAsync();
        var entity = new TerrainProfiles
        {
            ProjectId = projectId,
            StartPt = start,
            EndPt = end,
            LengthM = (decimal) totalDist,
            CreatedAt = DateTime.UtcNow
        };

        db.TerrainProfiles.Add(entity);
        await db.SaveChangesAsync(ct);

        for (var i = 0; i < points.Count; i++)
        {
            db.TerrainProfilePoints.Add(new Model.TerrainProfilePoints
            {
                ProfileId = entity.Id,
                Seq = i,
                DistM = (decimal) points[i].Distance,
                ElevM = (decimal) points[i].Elevation
            });
        }

        await db.SaveChangesAsync(ct);

        return new TerrainProfileData
        {
            Id = entity.Id,
            LengthM = entity.LengthM,
            Points = points
        };
    }
}