using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Infrastructure.Extensions
{
    public static class SwaggerExtensions
    {
        public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "GeoProfiles API",
                    Version = "v1"
                });

                c.ExampleFilters();
            });

            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .ToArray();
            services.AddSwaggerExamplesFromAssemblies(assemblies);

            return services;
        }

        public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "GeoProfiles API v1");
                c.DisplayRequestDuration();
            });

            return app;
        }
    }
}