using GeoProfiles.Model;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace GeoProfiles.Infrastructure.Services;

public readonly record struct ProfilePoint(double Distance, double Elevation);

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
        double samplingMeters = 10,
        CancellationToken ct = default);
}

public class TerrainProfileService(
    GeoProfilesContext db,
    MockElevationProvider elevProv)
    : ITerrainProfileService
{
    private static readonly GeographicCoordinateSystem Wgs84 = GeographicCoordinateSystem.WGS84;
    private static readonly ProjectedCoordinateSystem WebM = ProjectedCoordinateSystem.WebMercator;

    private static readonly MathTransform ToMerc = new CoordinateTransformationFactory()
        .CreateFromCoordinateSystems(Wgs84, WebM).MathTransform;

    private static Point ToMercator(Point p)
    {
        var c = ToMerc.Transform(new[] {p.X, p.Y});
        return new Point(c[0], c[1]) {SRID = 3857};
    }

    private const int OUT_N = 800; // точек, отдаваемых фронту

    public async Task<TerrainProfileData> BuildProfileAsync(
        Point start,
        Point end,
        Guid projectId,
        double _ = 10,
        CancellationToken ct = default)
    {
        var project = await db.Projects.FindAsync([projectId], ct)
                      ?? throw new KeyNotFoundException("Project not found");

        double totalDistM = ToMercator(start).Distance(ToMercator(end));

        var pathWgs = new LineString(new[]
        {
            new Coordinate(start.X, start.Y),
            new Coordinate(end.X, end.Y)
        }) {SRID = 4326};

        var nodes = elevProv.BuildNodes(pathWgs);

        double degLen = pathWgs.Length;
        double k = totalDistM / degLen;
        var xsNodes = nodes.Select(n => n.Dist * k).ToArray();
        var ysNodes = nodes.Select(n => n.Elev).ToArray();

        var spline = CubicSpline.CreatePchip(xsNodes, ysNodes);

        var xs = new double[OUT_N];
        var ys = new double[OUT_N];

        for (int i = 0; i < OUT_N; i++)
        {
            double x = totalDistM * i / (OUT_N - 1);
            xs[i] = x;
            ys[i] = spline.Interpolate(x);
        }

        var points = xs.Zip(ys, (d, h) => new ProfilePoint(d, h)).ToList();

        var entity = new TerrainProfiles
        {
            ProjectId = projectId,
            StartPt = start,
            EndPt = end,
            LengthM = (decimal) totalDistM,
            CreatedAt = DateTime.UtcNow
        };
        db.TerrainProfiles.Add(entity);
        await db.SaveChangesAsync(ct);

        db.TerrainProfilePoints.AddRange(
            points.Select((p, i) => new TerrainProfilePoints
            {
                ProfileId = entity.Id,
                Seq = i,
                DistM = (decimal) p.Distance,
                ElevM = (decimal) p.Elevation
            }));
        await db.SaveChangesAsync(ct);

        return new TerrainProfileData
        {
            Id = entity.Id,
            LengthM = entity.LengthM,
            Points = points
        };
    }
}