using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;

namespace GeoProfiles.Infrastructure.Services;

#pragma warning disable CS0618 // Type or member is obsolete

public sealed class IsolineGeneratorService : IIsolineGeneratorService
{
    private static readonly GeometryFactory Gf = new(new PrecisionModel(), 4326,
        CoordinateArraySequenceFactory.Instance);

    public Task<IEnumerable<Polygon>> GenerateAsync(BoundingBox bbox, int levels = 5, Guid? seed = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(levels, 2);

        var rng = seed.HasValue ? new Random(seed.Value.GetHashCode()) : Random.Shared;
        var result = new List<Polygon>(levels * 4);

        var desiredHills = rng.Next(2, 5);

        double minDim = Math.Min(bbox.Width, bbox.Height);
        double maxRGlobal = minDim * 0.25;
        double minRGlobal = minDim * 0.10;

        var hillCenters = new List<(double cx, double cy, double r)>();
        const int placementAttempts = 400;

        for (var attempt = 0; attempt < placementAttempts && hillCenters.Count < desiredHills; attempt++)
        {
            var r = rng.NextDouble() * (maxRGlobal - minRGlobal) + minRGlobal;

            var cx = rng.NextDouble() * (bbox.Width - 2 * r) + bbox.MinX + r;
            var cy = rng.NextDouble() * (bbox.Height - 2 * r) + bbox.MinY + r;

            var ok = true;
            var minGap = r * 1.6;
            foreach (var (ex, ey, er) in hillCenters)
            {
                if (Distance(ex, ey, cx, cy) < minGap + er)
                {
                    ok = false;
                    break;
                }
            }

            if (!ok) continue;

            hillCenters.Add((cx, cy, r));
        }

        if (hillCenters.Count == 1 && desiredHills > 1)
        {
            var existing = hillCenters[0];
            var r2 = existing.r * 0.6;
            var cx2 = bbox.MinX + bbox.Width * 0.75;
            var cy2 = bbox.MinY + bbox.Height * 0.25;
            hillCenters.Add((cx2, cy2, r2));
        }

        foreach (var (cx, cy, maxR) in hillCenters)
        {
            double ampFactor = 0.05 + rng.NextDouble() * 0.06;
            int verticesBase = 60 + rng.Next(0, 4) * 12;

            var freq1 = rng.Next(2, 7);
            var freq2 = freq1 + rng.Next(2, 5);
            var phase = rng.NextDouble() * Math.PI * 2;

            for (var lvl = levels - 1; lvl >= 0; lvl--)
            {
                double t = lvl / (double) (levels - 1);
                double baseR = maxR * (0.30 + 0.70 * t);
                double amplitude = baseR * ampFactor * t;
                int vertices = verticesBase + rng.Next(-6, 7);
                double step = Math.PI * 2 / vertices;

                var seq = Gf.CoordinateSequenceFactory.Create(vertices + 1, 2);
                for (var i = 0; i < vertices; i++)
                {
                    var angle = i * step;
                    var noise = (Math.Sin(angle * freq1 + phase) + Math.Sin(angle * freq2 + phase * 0.7)) / 2d;
                    var r = Math.Max(baseR * 0.2, baseR + noise * amplitude);
                    
                    seq.SetX(i, cx + r * Math.Cos(angle));
                    seq.SetY(i, cy + r * Math.Sin(angle));
                }

                seq.SetX(vertices, seq.GetX(0));
                seq.SetY(vertices, seq.GetY(0));

                var ring = new LinearRing(seq, Gf);
                var poly = new Polygon(ring, null, Gf);

                if (!poly.IsValid)
                {
                    var fixedPoly = poly.Buffer(0);
                    if (fixedPoly is Polygon {IsValid: true, IsEmpty: false} fp)
                        poly = fp;
                    else
                        continue;
                }

                result.Add(poly);
            }
        }

        result.Sort((a, b) => b.Area.CompareTo(a.Area));
        return Task.FromResult<IEnumerable<Polygon>>(result);
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public readonly record struct BoundingBox(double MinX, double MinY, double MaxX, double MaxY)
{
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
}

public interface IIsolineGeneratorService
{
    Task<IEnumerable<Polygon>> GenerateAsync(BoundingBox bbox, int levels = 5, Guid? seed = null);
}