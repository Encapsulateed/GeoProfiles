namespace GeoProfiles.Infrastructure.Settings;

public class JwtSettings
{
    public string Secret { get; set; } = default!;
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;

    public bool Enabled { get; set; } = false;

    public int ExpireMinutes { get; set; }
    public bool RequireHttpsMetadata { get; set; }
    public bool SaveToken { get; set; }
    public bool ValidateIssuer { get; set; }
    public bool ValidateAudience { get; set; }
    public bool ValidateIssuerSigningKey { get; set; }
    public bool ValidateLifetime { get; set; }
}