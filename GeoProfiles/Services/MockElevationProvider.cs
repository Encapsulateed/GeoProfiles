// GeoProfiles/Services/ContourLineElevationProvider.cs
using NetTopologySuite.Geometries;

namespace GeoProfiles.Services;

public sealed class ContourLineElevationProvider : IElevationProvider
{
    private const int    K            = 3;
    private const double OnLineTolDeg = 1e-5;
    private const double SearchRadDeg = 1e-3;

    private readonly IContourLineSpatialIndex _sp;

    public ContourLineElevationProvider(IContourLineSpatialIndex sp) => _sp = sp;

    public ValueTask<decimal> GetElevationAsync(Point pt, CancellationToken ct = default)
    {
        pt.SRID = 4326;

        if (!_sp.ProbablyInsideCoverage(pt))
            return new(decimal.Zero);

        var envExact = pt.EnvelopeInternal;
        envExact.ExpandBy(OnLineTolDeg);
        foreach (var cl in _sp.Query(envExact))
            if (cl.Geom!.IsWithinDistance(pt, OnLineTolDeg))
                return new((decimal)(cl.Level ?? 0));

        var env = pt.EnvelopeInternal;
        env.ExpandBy(SearchRadDeg);

        var cand = _sp.Query(env);
        if (cand.Count == 0)
            return new(decimal.Zero);

        var nearest = cand
            .Select(c => (dist: c.Geom!.Distance(pt), elev: c.Level ?? 0))
            .OrderBy(t => t.dist)
            .Take(K)
            .ToArray();

        if (nearest.Length == 1)
            return new((decimal)nearest[0].elev);

        double num = 0, den = 0;
        foreach (var (d, h) in nearest)
        {
            var w = 1.0 / Math.Max(d, 1e-10);
            num += w * h;
            den += w;
        }

        var res = (decimal)(num / den);
        return new(res);
    }
}
