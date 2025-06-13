using GeoProfiles.Services;

namespace GeoProfiles.Infrastructure.Extensions;

public static class ElevationExtensions
{
    public static IServiceCollection AddMockElevationProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IContourLineSpatialIndex, ContourLineSpatialIndex>();
        services.AddSingleton<IElevationProvider,       ContourLineElevationProvider>();

        return services;
    }
}