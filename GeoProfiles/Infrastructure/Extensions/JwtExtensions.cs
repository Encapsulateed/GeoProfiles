using System;
using GeoProfiles.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using GeoProfiles.Infrastructure.Settings;

namespace GeoProfiles.Infrastructure.Extensions
{
    public static class JwtExtensions
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services,
            IConfiguration configuration)
        {
            var section = configuration.GetSection("JwtSettings");
            services.Configure<JwtSettings>(section);
            var settings = section.Get<JwtSettings>();

            // необходим HttpClient для JWKS
            services.AddHttpClient();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = settings.RequireHttpsMetadata;
                    options.SaveToken = settings.SaveToken;

                    // настраиваем получение ключей через OpenID Connect metadata
                    var httpClient = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>()
                        .CreateClient();
                    var retriever = new HttpDocumentRetriever {RequireHttps = settings.RequireHttpsMetadata};
                    var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                        settings.SigningJwksUri,
                        new OpenIdConnectConfigurationRetriever(),
                        retriever);
                    options.ConfigurationManager = configManager;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKeyResolver = (_, _, _, _) =>
                            configManager.GetConfigurationAsync().Result.SigningKeys,
                        ValidateIssuer = settings.ValidateIssuer,
                        ValidIssuer = settings.Issuer,
                        ValidateAudience = settings.ValidateAudience,
                        ValidAudience = settings.Audience,
                        ValidateLifetime = settings.ValidateLifetime,
                        ClockSkew = TimeSpan.FromMinutes(settings.ClockSkewMinutes),
                        ValidAlgorithms = ["RSA256"]
                    };
                });

            services.AddSingleton<IJwtTokenService, JwtTokenService>();
            services.AddScoped<ITokenService, TokenService>();

            services.AddAuthorization();
            return services;
        }

        public static IApplicationBuilder UseJwtAuthentication(this IApplicationBuilder app)
        {
            app.UseAuthentication();
            app.UseAuthorization();
            return app;
        }
    }
}