using NetTopologySuite.Geometries;

namespace GeoProfiles.Services;

public interface IElevationProvider
{
    ValueTask<decimal> GetElevationAsync(Point pt, CancellationToken ct = default);
}