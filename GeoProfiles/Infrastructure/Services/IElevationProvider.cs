using NetTopologySuite.Geometries;

namespace GeoProfiles.Infrastructure.Services;

public interface IElevationProvider
{
    Task<decimal> GetElevationAsync(Point pt, CancellationToken ct = default);
}