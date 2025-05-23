using System.Security.Cryptography;
using GeoProfiles.Infrastructure.Settings;
using GeoProfiles.Model;
using Microsoft.Extensions.Options;

namespace GeoProfiles.Infrastructure.Services;

public interface ITokenService
{
    (string AccessToken, string RefreshToken)
        GenerateTokens(Guid userId, string username, string email, string[] roles);
}

public class TokenService(
    IJwtTokenService jwt,
    GeoProfilesContext db,
    IOptions<JwtSettings> opts)
    : ITokenService
{
    public (string AccessToken, string RefreshToken) GenerateTokens(Guid userId, string username, string email,
        string[] roles)
    {
        var access = jwt.CreateToken(userId, username, email, roles);

        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        db.RefreshTokens.Add(new RefreshTokens
        {
            UserId = userId,
            Token = refresh,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        db.SaveChanges();

        return (access, refresh);
    }
}