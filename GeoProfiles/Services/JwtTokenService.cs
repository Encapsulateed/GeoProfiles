using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GeoProfiles.Application.Auth;
using GeoProfiles.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GeoProfiles.Services;

public interface IJwtTokenService
{
    string CreateToken(Guid userId, string username, string email, string[]? roles);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly SecurityKey _key;

    public JwtTokenService(IOptions<JwtSettings> opts, IHttpClientFactory httpFactory)
    {
        ArgumentNullException.ThrowIfNull(opts);
        ArgumentNullException.ThrowIfNull(httpFactory);

        _settings = opts.Value;
        if (string.IsNullOrWhiteSpace(_settings.SigningJwksUri))
        {
            throw new InvalidOperationException("SigningJwksUri must be configured in JwtSettings.");
        }

        var client = httpFactory.CreateClient();
        var jwksJson = client.GetStringAsync(_settings.SigningJwksUri).GetAwaiter().GetResult();
        var jwkSet = new JsonWebKeySet(jwksJson);
        var jwk = jwkSet.Keys.SingleOrDefault(k => k.Kid == _settings.KeyId)
                  ?? throw new InvalidOperationException($"JWKS key '{_settings.KeyId}' not found.");

        _key = jwk;
    }

    public string CreateToken(Guid userId, string username, string email, string[]? roles)
    {
        var now = DateTime.UtcNow;
        var notBefore = now.AddSeconds(-1);
        var expires = now.AddMinutes(_settings.ExpireMinutes);

        var credentials = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory {CacheSignatureProviders = false}
        };

        var claims = new List<Claim>
        {
            new(Claims.UserId, userId.ToString()),
            new(Claims.UserName, username),
            new(Claims.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (roles != null && roles.Length > 0)
        {
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        }

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: notBefore,
            expires: expires,
            signingCredentials: credentials
        )
        {
            Header =
            {
                [JwtHeaderParameterNames.Kid] = _settings.KeyId
            }
        };

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}