using GeoProfiles.Infrastructure.Services;

namespace GeoProfiles.Infrastructure.Extensions;

public static class ProfilesExtensions
{
    public static IServiceCollection RegisterProfiles(this IServiceCollection services)
    {
        return services.AddScoped<ITerrainProfileService, TerrainProfileService>();
    }
}