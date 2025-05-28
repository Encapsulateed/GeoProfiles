using NetTopologySuite.Geometries;

namespace GeoProfiles.Infrastructure.Services;

public record Ring(Geometry Line, int Level);

public record Node(double Dist, double Elev);

public interface IElevationProvider
{
    Task<decimal> GetElevationAsync(Point pt, CancellationToken ct = default);
}