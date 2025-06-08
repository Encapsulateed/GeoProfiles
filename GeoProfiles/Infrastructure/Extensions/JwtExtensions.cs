using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using GeoProfiles.Infrastructure.Settings;
using GeoProfiles.Services;

namespace GeoProfiles.Infrastructure.Extensions;

    public static class JwtExtensions
    {
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services,
            IConfiguration configuration)
        {
            var section = configuration.GetSection("JwtSettings");
            services.Configure<JwtSettings>(section);
            var settings = section.Get<JwtSettings>();

            services.AddHttpClient();
            
            var sp = services.BuildServiceProvider();
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var jwksJson = http.GetStringAsync(settings.SigningJwksUri).GetAwaiter().GetResult();
            var jwkSet = new JsonWebKeySet(jwksJson);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    ArgumentNullException.ThrowIfNull(settings);

                    options.RequireHttpsMetadata = settings.RequireHttpsMetadata;
                    options.SaveToken = settings.SaveToken;

                    services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>()
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
                        IssuerSigningKeys = jwkSet.Keys,

                        ValidateIssuer = settings.ValidateIssuer,
                        ValidIssuer = settings.Issuer,

                        ValidateAudience = settings.ValidateAudience,
                        ValidAudience = settings.Audience,

                        ValidateLifetime = settings.ValidateLifetime,
                        ClockSkew = TimeSpan.FromMinutes(settings.ClockSkewMinutes),

                        ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
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
    