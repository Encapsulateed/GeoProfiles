using GeoProfiles.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

namespace GeoProfiles.Services;

public interface IContourLineSpatialIndex
{
    IList<ContourLines> Query(Envelope env);

    bool ProbablyInsideCoverage(Point p);
}

public sealed class ContourLineSpatialIndex : IContourLineSpatialIndex
{
    public  const double SearchRadDeg = 1e-3;   // ≈ 110 м
    private const double EnvelopeGrow = SearchRadDeg;

    private static readonly Lazy<(STRtree<ContourLines> Tree, Envelope AllEnv)>
        _lazy = new(BuildIndex, true);

    private static IServiceScopeFactory? _scopeFactory;
    public ContourLineSpatialIndex(IServiceScopeFactory sf) => _scopeFactory ??= sf;

    public IList<ContourLines> Query(Envelope env) => _lazy.Value.Tree.Query(env);

    public bool ProbablyInsideCoverage(Point p)
    {
        var env = new Envelope(_lazy.Value.AllEnv);
        env.ExpandBy(EnvelopeGrow);
        return env.Contains(p.Coordinate);
    }

    private static (STRtree<ContourLines>, Envelope) BuildIndex()
    {
        using var scope = _scopeFactory!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GeoProfilesContext>();

        var lines = db.ContourLines
                      .AsNoTracking()
                      .Take(1000)
                      .Where(l => l.Geom != null)
                      .ToList();

        foreach (var l in lines) l.Geom!.SRID = 4326;

        var tree   = new STRtree<ContourLines>();
        var allEnv = new Envelope();

        foreach (var l in lines)
        {
            var env = l.Geom!.EnvelopeInternal;
            tree.Insert(env, l);
            allEnv.ExpandToInclude(env);
        }
        tree.Build();

        return (tree, allEnv);
    }
}
