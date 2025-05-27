using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using System.Collections.Concurrent;


namespace GeoProfiles.Infrastructure.Services;

public class MockDemOptions
{
    public double StepHeight { get; set; } = 50; 
    public double JitterFactor { get; set; } = 1.0; 
}

public sealed class MockElevationProvider : IElevationProvider
{
    /* ──────────── тип для R-tree ──────────── */
    private sealed record Contour(Polygon Poly, int Level);

    private readonly STRtree<Contour> _tree = new();
    private readonly MockDemOptions _opts;
    private readonly ConcurrentDictionary<Coordinate, decimal> _cache = new();

    public MockElevationProvider(
        GeoProfilesContext db,
        IOptions<MockDemOptions> opts)
    {
        _opts = opts.Value;

        foreach (var iso in db.Isolines.Select(i => new {i.Geom, i.Level}))
        {
            if (iso.Geom is { } poly)
                _tree.Insert(poly.EnvelopeInternal,
                    new Contour(poly, iso.Level));
        }

        _tree.Build();
    }

    public Task<decimal> GetElevationAsync(Point pt, CancellationToken _ = default)
    {
        if (_cache.TryGetValue(pt.Coordinate, out var got))
            return Task.FromResult(got);

        var cand = _tree.Query(pt.EnvelopeInternal);

        var inner = cand
            .Where(c => c.Poly.Contains(pt))
            .OrderByDescending(c => c.Level)
            .FirstOrDefault();

        if (inner is null) 
            return Task.FromResult(0m);
        
        var outer = cand
            .Where(c => c.Level < inner.Level && c.Poly.Contains(inner.Poly))
            .OrderByDescending(c => c.Level)
            .FirstOrDefault();

        int Linner = inner.Level;
        int Louter = outer?.Level ?? Linner; 

        var center = inner.Poly.Centroid; 
        double r = pt.Distance(center);
        double R = outer?.Poly.Distance(center) ?? inner.Poly.Distance(center); 
        
        if (outer is not null)
        {
            var ray = new LineString(new[] {center.Coordinate, pt.Coordinate}) {SRID = 4326};
            var inter = ray.Intersection(outer.Poly);
            R = inter.IsEmpty
                ? outer.Poly.Distance(center)
                : center.Distance(inter.GetGeometryN(0));
        }

        if (R <= 0) R = inner.Poly.Distance(center); 
        double t = Math.Clamp(r / R, 0, 1);

        double step = _opts.StepHeight;
        double hInner = Linner * step;
        double hOuter = Louter * step;
        double elev = hInner + (hOuter - hInner) * (1 - t * t);

        if (_opts.JitterFactor > 0)
            elev += step * _opts.JitterFactor * (Random.Shared.NextDouble() - 0.5);

        var dec = (decimal) elev;
        _cache.TryAdd(pt.Coordinate, dec);
        return Task.FromResult(dec);
    }
}