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
        private static readonly ProjectedCoordinateSystem WebMercatorCs = ProjectedCoordinateSystem.WebMercator;

        private static readonly MathTransform ToMerc =
            new CoordinateTransformationFactory()
                .CreateFromCoordinateSystems(Wgs84, WebMercatorCs)
                .MathTransform;

        private static readonly MathTransform ToWgs =
            new CoordinateTransformationFactory()
                .CreateFromCoordinateSystems(WebMercatorCs, Wgs84)
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
            var c = ToMerc.Transform([p.X, p.Y]);
            return new Point(c[0], c[1]) {SRID = 3857};
        }

        public async Task<TerrainProfileData> BuildProfileAsync(
            Point start,
            Point end,
            Guid projectId,
            double samplingMeters = DefaultSampleM,
            CancellationToken ct = default)
        {
            const int outN = 400;

            var project = await _db.Projects
                              .FindAsync([projectId], ct)
                          ?? throw new KeyNotFoundException("Project not found");

            if (!project.Bbox.Contains(start) || !project.Bbox.Contains(end))
            {
                // TODO: handle reprojection or regeneration
            }

            var startM = ToMercator(start);
            var endM = ToMercator(end);
            var totalDistM = startM.Distance(endM);

            var lineM = new LineString([startM.Coordinate, endM.Coordinate])
                {SRID = 3857};
            var lineRef = new LengthIndexedLine(lineM);

            int nSamples = Math.Clamp(outN * 2, 2, MaxRawPoints);
            var rawPts = new Point[nSamples + 1];
            for (int i = 0; i <= nSamples; i++)
            {
                double frac = (double) i / nSamples;
                var ptM = lineRef.ExtractPoint(totalDistM * frac);
                var cWgs = ToWgs.Transform([ptM.X, ptM.Y]);
                rawPts[i] = new Point(cWgs[0], cWgs[1]) {SRID = 4326};
            }

            var heights = new double[rawPts.Length];
            for (int i = 0; i < rawPts.Length; i++)
            {
                heights[i] = (double) await _elevProv.GetElevationAsync(rawPts[i], ct);
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

            SavitzkyGolaySmooth(ys);

            var points = xs.Zip(ys, (d, h) => new ProfilePoint(d, h)).ToList();

            var entity = new TerrainProfiles
            {
                ProjectId = projectId,
                StartPt = start,
                EndPt = end,
                LengthM = (decimal) totalDistM,
                CreatedAt = DateTime.UtcNow
            };

            _db.TerrainProfiles.Add(entity);
            await _db.SaveChangesAsync(ct);

            _db.TerrainProfilePoints.AddRange(
                points.Select((p, i) => new TerrainProfilePoints()
                {
                    ProfileId = entity.Id,
                    Seq = i,
                    DistM = (decimal) p.Distance,
                    ElevM = (decimal) p.Elevation
                }));
            await _db.SaveChangesAsync(ct);

            return new TerrainProfileData
            {
                Id = entity.Id,
                LengthM = entity.LengthM,
                Points = points
            };
        }


        private static void SavitzkyGolaySmooth(double[] ys, int windowSize = 51, int polyOrder = 3)
        {
            if (windowSize % 2 == 0) throw new ArgumentException("windowSize must be odd");
            int half = windowSize / 2;
            int n = ys.Length;
            var result = new double[n];
            var V = new double[windowSize, polyOrder + 1];
            for (int i = -half; i <= half; i++)
            {
                double val = 1;
                for (int j = 0; j <= polyOrder; j++)
                {
                    V[i + half, j] = val;
                    val *= i;
                }
            }


            var VT = Transpose(V);
            var VTV = Multiply(VT, V);
            var invVTV = InvertSymmetric(VTV);
            var A = Multiply(invVTV, VT);
            var coeff = new double[windowSize];
            for (int i = 0; i < windowSize; i++)
                coeff[i] = A[0, i];

            for (int i = 0; i < n; i++)
            {
                double sum = 0;
                for (int k = -half; k <= half; k++)
                {
                    int idx = i + k;
                    if (idx < 0) idx = 0;
                    else if (idx >= n) idx = n - 1;
                    sum += coeff[k + half] * ys[idx];
                }

                result[i] = sum;
            }

            for (int i = 0; i < n; i++)
                ys[i] = result[i];
        }


        private static double[,] Transpose(double[,] M)
        {
            int r = M.GetLength(0), c = M.GetLength(1);
            var T = new double[c, r];
            for (int i = 0; i < r; i++)
            for (int j = 0; j < c; j++)
                T[j, i] = M[i, j];
            return T;
        }

        private static double[,] Multiply(double[,] A, double[,] B)
        {
            int r = A.GetLength(0), mid = A.GetLength(1), c = B.GetLength(1);
            var C = new double[r, c];
            for (int i = 0; i < r; i++)
            for (int j = 0; j < c; j++)
            for (int k = 0; k < mid; k++)
                C[i, j] += A[i, k] * B[k, j];
            return C;
        }

        /// <summary>
        /// Инвертирует маленькую симметричную матрицу методом Гаусса (или любым доступным вам).
        /// Предполагается, что размер небольшой (windowSize×windowSize).
        /// </summary>
        private static double[,] InvertSymmetric(double[,] M)
        {
            int n = M.GetLength(0);
            var A = new double[n, n];
            Array.Copy(M, A, M.Length);
            var inv = new double[n, n];
            for (int i = 0; i < n; i++)
                inv[i, i] = 1;
            // Простейшая реализация Гаусса
            for (int i = 0; i < n; i++)
            {
                double diag = A[i, i];
                for (int j = 0; j < n; j++)
                {
                    A[i, j] /= diag;
                    inv[i, j] /= diag;
                }

                for (int k = 0; k < n; k++)
                {
                    if (k == i) continue;
                    double factor = A[k, i];
                    for (int j = 0; j < n; j++)
                    {
                        A[k, j] -= factor * A[i, j];
                        inv[k, j] -= factor * inv[i, j];
                    }
                }
            }

            return inv;
        }
    }
}