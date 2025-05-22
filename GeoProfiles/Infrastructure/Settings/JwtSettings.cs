namespace GeoProfiles.Infrastructure.Settings;

public class JwtSettings
{
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public int ExpireMinutes { get; set; } = 60;
    public string SigningJwksUri { get; set; } = default!;
    public string KeyId { get; set; } = default!;

    public bool RequireHttpsMetadata { get; set; } = true;
    public bool SaveToken { get; set; } = true;
    public bool ValidateIssuer { get; set; } = true;
    public bool ValidateAudience { get; set; } = true;
    public bool ValidateLifetime { get; set; } = true;
    public int ClockSkewMinutes { get; set; } = 1;
}