using GeoProfiles.Infrastructure.Services;

namespace GeoProfiles.Infrastructure.Modules;

public static class IsolineModule
{
    public static IServiceCollection RegisterIsoline(
        this IServiceCollection services)
    {
        return services.AddTransient<IIsolineGeneratorService,
            IsolineGeneratorService>();
    }
}