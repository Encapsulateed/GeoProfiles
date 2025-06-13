// GeoProfiles/Services/TerrainProfileService.cs

using System.Collections.Concurrent;
using GeoProfiles.Model;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.LinearReferencing;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace GeoProfiles.Services;

/*────────────────────────────────────────── модели ──────────────────*/
public readonly record struct ProfilePoint(double Distance, double Elevation, bool IsOnIsoline);

public class TerrainProfileData
{
    public Guid Id { get; init; }
    public decimal LengthM { get; init; }
    public IReadOnlyList<ProfilePoint>? Points { get; init; }
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

/*────────────────────────────────────────── реализация ─────────────*/
public sealed class TerrainProfileService(
    GeoProfilesContext db,
    IElevationProvider elevProv) : ITerrainProfileService
{
    /*── CRS ───────────────────────────────────────────────────*/
    private static readonly GeographicCoordinateSystem Wgs84 = GeographicCoordinateSystem.WGS84;
    private static readonly ProjectedCoordinateSystem WebMercator = ProjectedCoordinateSystem.WebMercator;

    private static readonly MathTransform ToMerc =
        new CoordinateTransformationFactory()
            .CreateFromCoordinateSystems(Wgs84, WebMercator)
            .MathTransform;

    private static readonly MathTransform ToWgs =
        new CoordinateTransformationFactory()
            .CreateFromCoordinateSystems(WebMercator, Wgs84)
            .MathTransform;

    /*── параметры прореживания ───────────────────────────────*/
    private const double ClusterRadM = 20; // «метла» при сборе пересечений
    private const double MinIsolineGapM = 50; // окончательный минимум между маркерами
    private const int OutN = 400; // итоговых узлов профиля

    /*── прочие константы ─────────────────────────────────────*/
    private const double DefaultSampleM = 10;
    private const int MaxRawPoints = 20_000;
    private static readonly ConcurrentDictionary<(int, int), double[]> SavGolCache = new();

    /*──────────────────────── API ─────────────────────────────*/
    public async Task<TerrainProfileData> BuildProfileAsync(
        Point start,
        Point end,
        Guid projectId,
        double samplingMeters = DefaultSampleM,
        CancellationToken ct = default)
    {
        if (start.Distance(end) < 1e-5)
            throw new ArgumentException("Start and end points are too close");

        _ = await db.Projects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new KeyNotFoundException("Project not found");

        /*──── исходная линия в Web Mercator ───────────────────*/
        var p0M = ToMercator(start);
        var p1M = ToMercator(end);
        var lenM = p0M.Distance(p1M);
        if (lenM < samplingMeters)
            throw new ArgumentException("Distance is smaller than sampling step");

        var lineM = new LineString([p0M.Coordinate, p1M.Coordinate]) { SRID = 3857 };
        var lineRefM = new LengthIndexedLine(lineM);

        /*──── дискретизация отрезка ───────────────────────────*/
        int nRaw = Math.Clamp((int)Math.Ceiling(lenM / samplingMeters), 100, MaxRawPoints);
        var rawW = new Point[nRaw + 1];
        var rawM = new Point[nRaw + 1];

        for (int i = 0; i <= nRaw; i++)
        {
            double f = (double)i / nRaw;
            var ptM = lineRefM.ExtractPoint(lenM * f);
            rawM[i] = new Point(ptM.X, ptM.Y) { SRID = 3857 };

            var wgs = ToWgs.Transform([ptM.X, ptM.Y]);
            rawW[i] = new Point(wgs[0], wgs[1]) { SRID = 4326 };
        }

        /*──── высоты (параллельно) ───────────────────────────*/
        var elev = new double[rawW.Length];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, rawW.Length),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2, CancellationToken = ct },
            async (i, token) => elev[i] = (double)await elevProv.GetElevationAsync(rawW[i], token));

        /*──── координаты X для сырого ряда ───────────────────*/
        var xRaw = new double[rawM.Length];
        xRaw[0] = 0;
        for (int i = 1; i < rawM.Length; i++)
            xRaw[i] = xRaw[i - 1] + rawM[i - 1].Distance(rawM[i]);

        /*──── PCHIP-интерполяция + сглаживание ───────────────*/
        var spline = CubicSpline.CreatePchip(xRaw, elev);

        var xs = new double[OutN];
        var ys = new double[OutN];
        for (int i = 0; i < OutN; i++)
        {
            double x = lenM * i / (OutN - 1);
            xs[i] = x;
            ys[i] = spline.Interpolate(x);
        }

        SavGolSmooth(ys);

        /*───────────────── пересечения с изолиниями ──────────*/
        var profWgs = new LineString([start.Coordinate, end.Coordinate]) { SRID = 4326 };
        var env = profWgs.EnvelopeInternal.Copy();
        env.ExpandBy(1e-5);

        var allHits = new List<double>(); // расстояния вдоль профиля

        foreach (var cl in await db.ContourLines.AsNoTracking()
                     .Where(c => c.Geom != null)
                     .ToListAsync(ct))
        {
            var g = cl.Geom!;
            g.SRID = 4326;
            if (!g.EnvelopeInternal.Intersects(env)) continue;

            var inter = g.Intersection(profWgs);
            if (inter.IsEmpty) continue;

            foreach (var p in PointsOf(inter))
            {
                var pm = ToMerc.Transform([p.X, p.Y]);
                allHits.Add(lineRefM.Project(new Coordinate(pm[0], pm[1])));
            }
        }

        /*──── метла 20 м: оставляем по одной точке на кластер ──*/
        allHits.Sort();
        var accepted = new List<double>();

        foreach (double d in allHits)
            if (accepted.All(a => Math.Abs(a - d) >= ClusterRadM))
                accepted.Add(d);

        /*──── флаги узлов профиля ─────────────────────────────*/
        var flags = new bool[OutN];
        foreach (double dist in accepted)
        {
            int idx = (int)Math.Round(dist / lenM * (OutN - 1));
            idx = Math.Clamp(idx, 0, OutN - 1);
            flags[idx] = true;
        }

        var pts = Enumerable.Range(0, xs.Length)
            .Select(i => new ProfilePoint(xs[i], ys[i], flags[i]))
            .ToList();

        double last = double.NegativeInfinity;
        for (int i = 0; i < pts.Count; i++)
        {
            if (!pts[i].IsOnIsoline) continue;

            if (pts[i].Distance - last < MinIsolineGapM)
                pts[i] = new ProfilePoint(pts[i].Distance, pts[i].Elevation, false);
            else
                last = pts[i].Distance;
        }

        return SaveAndReturn(start, end, lenM, pts, projectId);
    }

    private TerrainProfileData SaveAndReturn(
        Point start, Point end, double lenM,
        List<ProfilePoint> pts,
        Guid projectId)
    {
        var ent = new TerrainProfiles
        {
            ProjectId = projectId,
            StartPt = start,
            EndPt = end,
            LengthM = (decimal)lenM,
            CreatedAt = DateTime.UtcNow
        };

        db.Database.CreateExecutionStrategy().Execute(() =>
        {
            using var tx = db.Database.BeginTransaction();
            db.TerrainProfiles.Add(ent);
            db.SaveChanges();

            db.TerrainProfilePoints.AddRange(
                pts.Select((p, i) => new TerrainProfilePoints
                {
                    ProfileId = ent.Id,
                    Seq = i,
                    DistM = (decimal)p.Distance,
                    ElevM = (decimal)p.Elevation
                }));
            db.SaveChanges();
            tx.Commit();
        });

        return new TerrainProfileData
        {
            Id = ent.Id,
            LengthM = ent.LengthM,
            Points = pts
        };
    }

    private static Point ToMercator(Point p)
    {
        var c = ToMerc.Transform([p.X, p.Y]);
        return new Point(c[0], c[1]) { SRID = 3857 };
    }

    private static IEnumerable<Point> PointsOf(Geometry g)
    {
        if (g.IsEmpty) yield break;
        switch (g)
        {
            case Point p: yield return p; break;
            case MultiPoint mp:
                for (int i = 0; i < mp.NumGeometries; i++) yield return (Point)mp.GetGeometryN(i);
                break;
            case GeometryCollection gc:
                for (int i = 0; i < gc.NumGeometries; i++)
                    foreach (var q in PointsOf(gc.GetGeometryN(i)))
                        yield return q;
                break;
        }
    }

    private static void SavGolSmooth(double[] y, int win = 61, int poly = 3)
    {
        if (win % 2 == 0) throw new ArgumentException(nameof(win));
        var c = SavGolCache.GetOrAdd((win, poly), _ => CalcCoeffs(win, poly));

        int h = win / 2;
        var t = new double[y.Length];

        for (int i = 0; i < y.Length; i++)
        {
            double s = 0;
            for (int k = -h; k <= h; k++)
            {
                int idx = Math.Clamp(i + k, 0, y.Length - 1);
                s += c[k + h] * y[idx];
            }

            t[i] = s;
        }

        Buffer.BlockCopy(t, 0, y, 0, y.Length * sizeof(double));
    }

    private static double[] CalcCoeffs(int win, int p)
    {
        int h = win / 2;
        var V = new double[win, p + 1];
        for (int i = -h; i <= h; i++)
        {
            double v = 1;
            for (int j = 0; j <= p; j++)
            {
                V[i + h, j] = v;
                v *= i;
            }
        }

        var A = Mul(Inv(Mul(Tr(V), V)), Tr(V));
        var res = new double[win];
        for (int i = 0; i < win; i++) res[i] = A[0, i];
        return res;
    }

    private static double[,] Tr(double[,] M)
    {
        int r = M.GetLength(0), c = M.GetLength(1);
        var T = new double[c, r];
        for (int i = 0; i < r; i++)
        for (int j = 0; j < c; j++)
            T[j, i] = M[i, j];
        return T;
    }

    private static double[,] Mul(double[,] A, double[,] B)
    {
        int r = A.GetLength(0), k = A.GetLength(1), c = B.GetLength(1);
        var C = new double[r, c];
        for (int i = 0; i < r; i++)
        for (int j = 0; j < c; j++)
        for (int t = 0; t < k; t++)
            C[i, j] += A[i, t] * B[t, j];
        return C;
    }

    private static double[,] Inv(double[,] M)
    {
        int n = M.GetLength(0);
        var A = new double[n, n];
        Array.Copy(M, A, M.Length);
        var I = new double[n, n];
        for (int i = 0; i < n; i++) I[i, i] = 1;

        for (int i = 0; i < n; i++)
        {
            double d = A[i, i];
            for (int j = 0; j < n; j++)
            {
                A[i, j] /= d;
                I[i, j] /= d;
            }

            for (int k = 0; k < n; k++)
            {
                if (k == i) continue;
                double f = A[k, i];
                for (int j = 0; j < n; j++)
                {
                    A[k, j] -= f * A[i, j];
                    I[k, j] -= f * I[i, j];
                }
            }
        }

        return I;
    }
}