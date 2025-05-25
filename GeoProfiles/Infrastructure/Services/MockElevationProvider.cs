using GeoProfiles.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Point = NetTopologySuite.Geometries.Point;

namespace GeoProfiles.Infrastructure.Services;

public class MockDemOptions
{
    public double StepHeight { get; set; } = 50;

    public double JitterFactor { get; set; } = 0.1;
}

public class MockElevationProvider(
    GeoProfilesContext db,
    IOptions<MockDemOptions> opts) : IElevationProvider
{
    private readonly Random _rnd = new Random();
    private readonly MockDemOptions _opts = opts.Value;

    public async Task<decimal> GetElevationAsync(Point pt, CancellationToken cancellationToken = default)
    {
        pt.SRID = 4326;

        var cached = await db.ElevationCache
            .FindAsync([pt], cancellationToken);

        if (cached != null)
            return cached.ElevM;

        var level = await db.Isolines
            .Where(i => i.Geom.Contains(pt))
            .Select(i => (int?) i.Level)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        var baseHeight = level * _opts.StepHeight;

        var jitter = (_rnd.NextDouble() - 0.5)
                     * _opts.StepHeight
                     * _opts.JitterFactor;

        var elev = Convert.ToDecimal(baseHeight + jitter);

        var entry = new ElevationCache
        {
            Pt = pt,
            ElevM = elev,
            UpdatedAt = DateTime.UtcNow
        };

        db.ElevationCache.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        return elev;
    }
}