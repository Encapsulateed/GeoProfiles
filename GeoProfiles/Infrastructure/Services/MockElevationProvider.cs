using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.LinearReferencing;

namespace GeoProfiles.Infrastructure.Services;

public class MockDemOptions
{
    public double StepHeight { get; set; } = 50;

    public double JitterFactor { get; set; } = 0.1;
}

public sealed class MockElevationProvider : IElevationProvider
{
    private readonly double _step;
    private readonly STRtree<Ring> _tree = new();
    private readonly Dictionary<long, Node[]> _profileCache = new();
    private readonly List<Ring> _allRings = new();

    public MockElevationProvider(
        GeoProfilesContext db,
        IOptions<MockDemOptions> opts)
    {
        _step = opts.Value.StepHeight;

        foreach (var iso in db.Isolines.Select(i => new {i.Geom, i.Level}))
        {
            var line = iso.Geom.Boundary;
            _tree.Insert(line.EnvelopeInternal, new Ring(line, iso.Level));
        }

        foreach (var iso in db.Isolines.Select(i => new {i.Geom, i.Level}))
        {
            var line = iso.Geom.Boundary;
            var ring = new Ring(line, iso.Level);

            _tree.Insert(line.EnvelopeInternal, ring);
            _allRings.Add(ring);
        }

        _tree.Build();
    }

    public Task<decimal> GetElevationAsync(
        Point pt,
        CancellationToken ct = default)
        => Task.FromResult(0m);

    public Node[] BuildNodes(LineString path)
    {
        long key = path.GetHashCode();
        if (_profileCache.TryGetValue(key, out var cached)) return cached;

        var li = new LengthIndexedLine(path);
        var list = new List<Node>();

        foreach (var ring in _tree.Query(path.EnvelopeInternal))
        {
            var inter = ring.Line.Intersection(path);
            for (int i = 0; i < inter.NumGeometries; i++)
                if (inter.GetGeometryN(i) is Point p)
                    list.Add(new Node(li.IndexOf(p.Coordinate), ring.Level * _step));
        }

        double lenDeg = path.Length;

        list.Add(new Node(0,
            EstimateElev(path.StartPoint)));

        list.Add(new Node(lenDeg,
            EstimateElev(path.EndPoint)));

        if (list.Count <= 2)
        {
            double midFrac = 0.5;
            Coordinate cMid = li.ExtractPoint(lenDeg * midFrac);
            var pMid = new Point(cMid) {SRID = 4326};
            double hFlat = EstimateElev(pMid);

            list.Clear();
            list.Add(new Node(0, hFlat));
            list.Add(new Node(lenDeg, hFlat));
        }

        var nodes = list
            .GroupBy(n => n.Dist)
            .Select(g => new Node(g.Key, g.Max(n => n.Elev)))
            .OrderBy(n => n.Dist)
            .ToArray();

        _profileCache[key] = nodes;
        return nodes;
    }

    private double EstimateElev(Point pt)
    {
        const int maxDistM = 500;

        var cand = _tree.Query(pt.EnvelopeInternal);
        if (cand.Count == 0) cand = _allRings;

        Ring? inner = null;
        double dInner = double.MaxValue;
        Ring? outer = null;
        double dOuter = double.MaxValue;

        foreach (var r in cand)
        {
            double d = r.Line.Distance(pt);
            if (r.Level >= (inner?.Level ?? int.MinValue) && d < dInner)
            {
                inner = r;
                dInner = d;
            }

            if (r.Level <= (outer?.Level ?? int.MaxValue) && d < dOuter)
            {
                outer = r;
                dOuter = d;
            }
        }

        double bestDeg = Math.Min(dInner, dOuter);
        double bestM = bestDeg * 111_000;
        if (bestM > maxDistM || (inner == null && outer == null))
            return 0;

        if (inner == null || outer == null || inner.Level == outer.Level)
            return ((inner ?? outer)!).Level * _step;

        double frac = dInner / (dInner + dOuter);
        return outer.Level * _step + (inner.Level - outer.Level) * _step * (1 - frac);
    }
}