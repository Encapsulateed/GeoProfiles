using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeoProfiles.Model;
using GeoProfiles.Model.Dto;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.LinearReferencing;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace GeoProfiles.Infrastructure.Services
{
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

    public class TerrainProfileService : ITerrainProfileService
    {
        private readonly GeoProfilesContext _db;
        private readonly IElevationProvider _elevProv;

        private static readonly GeographicCoordinateSystem Wgs84 = GeographicCoordinateSystem.WGS84;
        private static readonly ProjectedCoordinateSystem WebMercatorCS = ProjectedCoordinateSystem.WebMercator;
        private static readonly MathTransform ToMerc =
            new CoordinateTransformationFactory()
                .CreateFromCoordinateSystems(Wgs84, WebMercatorCS)
                .MathTransform;
        private static readonly MathTransform ToWgs =
            new CoordinateTransformationFactory()
                .CreateFromCoordinateSystems(WebMercatorCS, Wgs84)
                .MathTransform;

        private const double DefaultSampleM = 10;
        private const int MaxRawPoints = 20_000;

        public TerrainProfileService(
            GeoProfilesContext db,
            IElevationProvider elevProv)
        {
            _db = db;
            _elevProv = elevProv;
        }

        private static Point ToMercator(Point p)
        {
            var c = ToMerc.Transform(new[] { p.X, p.Y });
            return new Point(c[0], c[1]) { SRID = 3857 };
        }

        public async Task<TerrainProfileData> BuildProfileAsync(
            Point start,
            Point end,
            Guid projectId,
            double samplingMeters = DefaultSampleM,
            CancellationToken ct = default)
        {
            const int outN = 800;

            var project = await _db.Projects
                .FindAsync(new object[] { projectId }, ct)
                ?? throw new KeyNotFoundException("Project not found");

            if (!project.Bbox.Contains(start) || !project.Bbox.Contains(end))
            {
                // TODO: handle reprojection or regeneration
            }

            var startM = ToMercator(start);
            var endM   = ToMercator(end);
            var totalDistM = startM.Distance(endM);

            var lineM = new LineString(new[] { startM.Coordinate, endM.Coordinate })
            { SRID = 3857 };
            var lineRef = new LengthIndexedLine(lineM);

            int nSamples = Math.Clamp(outN * 2, 2, MaxRawPoints);
            var rawPts = new Point[nSamples + 1];
            for (int i = 0; i <= nSamples; i++)
            {
                double frac = (double)i / nSamples;
                var ptM = lineRef.ExtractPoint(totalDistM * frac);
                var cWgs = ToWgs.Transform(new[] { ptM.X, ptM.Y });
                rawPts[i] = new Point(cWgs[0], cWgs[1]) { SRID = 4326 };
            }

            var heights = new double[rawPts.Length];
            for (int i = 0; i < rawPts.Length; i++)
            {
                heights[i] = (double)await _elevProv.GetElevationAsync(rawPts[i], ct);
            }

            var x = new double[rawPts.Length];
            x[0] = 0;
            for (int i = 1; i < rawPts.Length; i++)
            {
                var p0 = ToMercator(rawPts[i - 1]);
                var p1 = ToMercator(rawPts[i]);
                x[i] = x[i - 1] + p0.Distance(p1);
            }

            var spline = CubicSpline.CreatePchip(x, heights);
            var xs = new double[outN];
            var ys = new double[outN];
            for (int i = 0; i < outN; i++)
            {
                double xx = totalDistM * i / (outN - 1);
                xs[i] = xx;
                ys[i] = spline.Interpolate(xx);
            }

            SplineSmooth(xs, ys, minStepM: 1000);

            var points = xs.Zip(ys, (d, h) => new ProfilePoint(d, h)).ToList();

            var entity = new TerrainProfiles()
            {
                ProjectId = projectId,
                StartPt = start,
                EndPt = end,
                LengthM = (decimal)totalDistM,
                CreatedAt = DateTime.UtcNow
            };
            _db.TerrainProfiles.Add(entity);
            await _db.SaveChangesAsync(ct);

            _db.TerrainProfilePoints.AddRange(
                points.Select((p, i) => new TerrainProfilePoints()
                {
                    ProfileId = entity.Id,
                    Seq     = i,
                    DistM   = (decimal)p.Distance,
                    ElevM   = (decimal)p.Elevation
                }));
            await _db.SaveChangesAsync(ct);

            return new TerrainProfileData
            {
                Id = entity.Id,
                LengthM = entity.LengthM,
                Points = points
            };
        }

        private static void SplineSmooth(double[] xs, double[] ys, double minStepM = 40)
        {
            var xKnots = new List<double> { xs[0] };
            var yKnots = new List<double> { ys[0] };

            for (int i = 1; i < xs.Length - 1; i++)
            {
                if (xs[i] - xKnots[^1] >= minStepM)
                {
                    xKnots.Add(xs[i]);
                    yKnots.Add(ys[i]);
                }
            }

            xKnots.Add(xs[^1]);
            yKnots.Add(ys[^1]);

            var spline = CubicSpline.CreatePchip(xKnots.ToArray(), yKnots.ToArray());
            for (int i = 0; i < xs.Length; i++)
            {
                ys[i] = spline.Interpolate(xs[i]);
            }
        }
    }
}
