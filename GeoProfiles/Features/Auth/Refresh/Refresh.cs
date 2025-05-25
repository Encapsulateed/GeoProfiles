using System.Net.Mime;
using FluentValidation;
using GeoProfiles.Infrastructure;
using GeoProfiles.Infrastructure.Examples;
using GeoProfiles.Infrastructure.Services;
using GeoProfiles.Model.Dto;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Auth.Refresh;

public class Refresh(
    GeoProfilesContext db,
    ITokenService tokenService,
    ILogger<Refresh> logger) : ControllerBase
{
    [HttpPost("api/v1/auth/refresh")]
    [Produces(MediaTypeNames.Application.Json)]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(RefreshDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [SwaggerRequestExample(typeof(RefreshRequest), typeof(RefreshRequestExample))]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(RefreshDtoExample))]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(BadRequestExample))]
    public async Task<IActionResult> Action([FromBody] RefreshRequest request)
    {
        logger.LogInformation("Refreshing token...");

        new Validator().ValidateAndThrow(request);

        var stored = await db.RefreshTokens
            .SingleOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (stored?.IsRevoked != false || stored.ExpiresAt <= DateTime.UtcNow)
        {
            return BadRequest(new ErrorResponse(
                ErrorCode: "invalid_refresh_token",
                ErrorMessage: "Refresh token is invalid or expired."));
        }

        var user = await db.Users
            .FindAsync(stored.UserId);

        if (user is null)
        {
            return BadRequest(new ErrorResponse(
                ErrorCode: "user_not_found",
                ErrorMessage: "User not found for given refresh token."));
        }

        stored.IsRevoked = true;
        db.RefreshTokens.Update(stored);

        var (newAccess, newRefresh) = tokenService.GenerateTokens(user.Id, user.Username, user.Email, []);

        await db.SaveChangesAsync();

        logger.LogInformation("Successfully refreshed token.");

        return Ok(new RefreshDto
        {
            Token = newAccess,
            RefreshToken = newRefresh
        });
    }

    private class Validator : AbstractValidator<RefreshRequest>
    {
        public Validator()
        {
            RuleFor(r => r.RefreshToken).NotEmpty();
        }
    }
}