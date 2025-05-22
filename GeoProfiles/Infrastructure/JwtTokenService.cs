using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GeoProfiles.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GeoProfiles.Infrastructure;

public interface IJwtTokenService
{
    string CreateToken(Guid userId, string username, string[] roles);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly SecurityKey _key;

    public JwtTokenService(IOptions<JwtSettings> opts, IHttpClientFactory httpFactory)
    {
        _settings = opts.Value;
        var client = httpFactory.CreateClient();
        var jwksJson = client.GetStringAsync(_settings.SigningJwksUri).GetAwaiter().GetResult();
        var jwkSet = new JsonWebKeySet(jwksJson);
        var jwk = jwkSet.Keys.Single(k => k.Kid == _settings.KeyId);
        _key = jwk;
    }

    public string CreateToken(Guid userId, string username, string[] roles)
    {
        var now = DateTime.UtcNow;
        var notBefore = now.AddSeconds(-1);
        var expires = now.AddMinutes(_settings.ExpireMinutes);

        var creds = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory {CacheSignatureProviders = false}
        };

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: notBefore,
            expires: expires,
            signingCredentials: creds
        );
        token.Header[JwtHeaderParameterNames.Kid] = _settings.KeyId;
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}