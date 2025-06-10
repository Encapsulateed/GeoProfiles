using System.Collections.Concurrent;
using GeoProfiles.Model;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.LinearReferencing;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace GeoProfiles.Services
{
    /// <summary>
    /// Точка профиля:
    /// • <paramref name="Distance"/> — расстояние от начала, м
    /// • <paramref name="Elevation"/> — высота, м
    /// • <paramref name="IsOnIsoline"/> — лежит ли точка на изолинии
    /// </summary>
    public readonly record struct ProfilePoint(
        double Distance,
        double Elevation,
        bool   IsOnIsoline);

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

    /// <summary>
    /// Построитель продольного профиля местности.
    /// Берёт высоту из IElevationProvider (теперь ContourLineElevationProvider)
    /// и помечает точки, находящиеся на изолинии.
    /// </summary>
    public class TerrainProfileService(
        GeoProfilesContext db,
        IElevationProvider elevProv) : ITerrainProfileService
    {
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
        private const int MaxRawPoints      = 20_000;
        private const int OutN              = 400;       // финальное кол-во точек

        private static readonly ConcurrentDictionary<(int, int), double[]> SavGolCoefficientsCache = new();

        private static Point ToMercator(Point p)
        {
            var c = ToMerc.Transform([p.X, p.Y]);
            return new Point(c[0], c[1]) { SRID = 3857 };
        }

        public async Task<TerrainProfileData> BuildProfileAsync(
            Point start,
            Point end,
            Guid projectId,
            double samplingMeters = DefaultSampleM,
            CancellationToken ct = default)
        {
            if (start.Distance(end) < 1e-5)
                throw new ArgumentException("Start and end points are too close");

            // ── проверка, что профиль внутри проекта ─────────────────────────────
            var project = await db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId, ct)
                ?? throw new KeyNotFoundException("Project not found");

            if (!project.Bbox.Contains(start) || !project.Bbox.Contains(end))
            {
                // Для MVP просто пропускаем; можно добавить репроекцию позже
            }

            // ── перевод в Меркатор, расчёт длины ─────────────────────────────────
            var startM     = ToMercator(start);
            var endM       = ToMercator(end);
            var totalDistM = startM.Distance(endM);

            if (totalDistM < samplingMeters)
                throw new ArgumentException("Distance is smaller than sampling step");

            // ── дискретизация ломаной ────────────────────────────────────────────
            var lineM  = new LineString([startM.Coordinate, endM.Coordinate]) { SRID = 3857 };
            var lineRef = new LengthIndexedLine(lineM);

            int nSamples = CalculateAdaptiveSamples(totalDistM, samplingMeters);
            var rawPts     = new Point[nSamples + 1];   // WGS-84
            var mercatorPt = new Point[nSamples + 1];   // WebMercator

            for (int i = 0; i <= nSamples; i++)
            {
                double frac = (double)i / nSamples;
                var ptM = lineRef.ExtractPoint(totalDistM * frac);

                mercatorPt[i] = new Point(ptM.X, ptM.Y) { SRID = 3857 };

                var cWgs = ToWgs.Transform([ptM.X, ptM.Y]);
                rawPts[i] = new Point(cWgs[0], cWgs[1]) { SRID = 4326 };
            }

            // ── получаем высоты параллельно ─────────────────────────────────────
            var heights = new double[rawPts.Length];

            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 2,
                CancellationToken      = ct
            };

            await Parallel.ForEachAsync(
                Enumerable.Range(0, rawPts.Length),
                options,
                async (i, token) =>
                {
                    heights[i] = (double)await elevProv.GetElevationAsync(rawPts[i], token);
                });

            // ── расстояния вдоль линии для сырых точек ──────────────────────────
            var x = new double[rawPts.Length];
            x[0] = 0;
            for (int i = 1; i < rawPts.Length; i++)
            {
                x[i] = x[i - 1] + mercatorPt[i - 1].Distance(mercatorPt[i]);
            }

            // ── интерполяция сплайном и сглаживание ─────────────────────────────
            var spline = CubicSpline.CreatePchip(x, heights);

            var xs      = new double[OutN];
            var ys      = new double[OutN];
            var crosses = new bool  [OutN];

            for (int i = 0; i < OutN; i++)
            {
                double xx = totalDistM * i / (OutN - 1);
                xs[i] = xx;
                ys[i] = spline.Interpolate(xx);

                // Проверка пересечения изолинии (точность ~1 м)
                var ptM  = lineRef.ExtractPoint(xx);
                var cWgs = ToWgs.Transform([ptM.X, ptM.Y]);
                var ptW  = new Point(cWgs[0], cWgs[1]) { SRID = 4326 };

                crosses[i] = await db.ContourLines
                    .AsNoTracking()
                    .AnyAsync(cl =>
                        cl.Geom != null &&
                        cl.Geom.IsWithinDistance(ptW, 1e-5), ct);
            }

            FastSavitzkyGolaySmooth(ys);

            var points = Enumerable.Range(0, OutN)
                .Select(i => new ProfilePoint(xs[i], ys[i], crosses[i]))
                .ToList();

            // ── сохраняем профиль в БД ──────────────────────────────────────────
            var entity = new TerrainProfiles
            {
                ProjectId = projectId,
                StartPt   = start,
                EndPt     = end,
                LengthM   = (decimal)totalDistM,
                CreatedAt = DateTime.UtcNow
            };

            using var transaction = await db.Database.BeginTransactionAsync(ct);

            db.TerrainProfiles.Add(entity);
            await db.SaveChangesAsync(ct);

            db.TerrainProfilePoints.AddRange(
                points.Select((p, i) => new TerrainProfilePoints
                {
                    ProfileId = entity.Id,
                    Seq       = i,
                    DistM     = (decimal)p.Distance,
                    ElevM     = (decimal)p.Elevation,
                    IsOnIsoline = p.IsOnIsoline
                }));

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new TerrainProfileData
            {
                Id       = entity.Id,
                LengthM  = entity.LengthM,
                Points   = points
            };
        }

        // ──────────────────────────────────────────────────────────────────────────
        private static int CalculateAdaptiveSamples(double totalDistance, double samplingStep)
        {
            int baseSamples = (int)Math.Ceiling(totalDistance / samplingStep);
            return Math.Clamp(baseSamples, 100, MaxRawPoints);
        }

        private static void FastSavitzkyGolaySmooth(double[] ys, int windowSize = 61, int polyOrder = 3)
        {
            if (windowSize % 2 == 0)
                throw new ArgumentException("windowSize must be odd");

            var coefficients = SavGolCoefficientsCache.GetOrAdd(
                (windowSize, polyOrder),
                key => CalculateSavGolCoefficients(key.Item1, key.Item2));

            int half = windowSize / 2;
            int n    = ys.Length;
            var sm   = new double[n];

            for (int i = 0; i < n; i++)
            {
                double sum = 0;
                for (int k = -half; k <= half; k++)
                {
                    int idx = i + k;
                    if (idx < 0)       idx = 0;
                    else if (idx >= n) idx = n - 1;
                    sum += coefficients[k + half] * ys[idx];
                }

                sm[i] = sum;
            }

            Buffer.BlockCopy(sm, 0, ys, 0, n * sizeof(double));
        }

        // ── вычисление коэффициентов Савицкого–Голея (кешируется) ────────────────
        private static double[] CalculateSavGolCoefficients(int windowSize, int polyOrder)
        {
            int half = windowSize / 2;
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

            var VT     = Transpose(V);
            var VTV    = Multiply(VT, V);
            var invVTV = InvertSymmetric(VTV);
            var A      = Multiply(invVTV, VT);

            var coeff = new double[windowSize];
            for (int i = 0; i < windowSize; i++)
                coeff[i] = A[0, i];

            return coeff;
        }

        // ── маленькая линейная алгебра (без внешних пакетов) ────────────────────
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

        private static double[,] InvertSymmetric(double[,] M)
        {
            int n = M.GetLength(0);
            var A   = new double[n, n];
            Array.Copy(M, A, M.Length);
            var inv = new double[n, n];
            for (int i = 0; i < n; i++)
                inv[i, i] = 1;

            for (int i = 0; i < n; i++)
            {
                double diag = A[i, i];
                for (int j = 0; j < n; j++)
                {
                    A[i, j]   /= diag;
                    inv[i, j] /= diag;
                }

                for (int k = 0; k < n; k++)
                {
                    if (k == i) continue;
                    double factor = A[k, i];
                    for (int j = 0; j < n; j++)
                    {
                        A[k, j]   -= factor * A[i, j];
                        inv[k, j] -= factor * inv[i, j];
                    }
                }
            }

            return inv;
        }
    }
}
