using System.Net.Mime;
using GeoProfiles.Application.Auth;
using GeoProfiles.Infrastructure;
using GeoProfiles.Infrastructure.Examples;
using GeoProfiles.Model.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Auth.Me;

public class Me(GeoProfilesContext db, ILogger<Me> logger) : ControllerBase
{
    [HttpGet("api/v1/auth/me")]
    [Authorize]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(UserDtoExample))]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(BadRequestExample))]
    public async Task<IActionResult> Action()
    {
        logger.LogInformation("Getting user info");

        var claims = User.Claims.ToList();

        var sub = claims
            .FirstOrDefault(c => c.Type == Claims.UserId)?
            .Value;

        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
        {
            logger.LogInformation("User is not authorized");

            return Unauthorized(new Errors.UserUnauthorized("Unauthorized"));
        }

        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            logger.LogInformation("User not found");

            return NotFound(new Errors.UserNotFound("User not found"));
        }

        return Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email
        });
    }
}