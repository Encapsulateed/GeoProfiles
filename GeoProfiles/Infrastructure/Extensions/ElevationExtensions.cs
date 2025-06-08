using GeoProfiles.Services;

namespace GeoProfiles.Infrastructure.Extensions;

public static class ElevationExtensions
{
    public static IServiceCollection AddMockElevationProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<MockDemOptions>()
            .Bind(configuration.GetSection("Dem"))
            .ValidateDataAnnotations();

        services
            .AddTransient<IElevationProvider,MockElevationProvider>();

        return services;
    }
}