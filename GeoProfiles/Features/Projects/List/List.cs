using System.Net.Mime;
using GeoProfiles.Application.Auth;
using GeoProfiles.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Projects.List;

public class List(
    GeoProfilesContext db,
    ILogger<List> logger) : ControllerBase
{
    [HttpGet("api/v1/project/list")]
    [Authorize]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(ProjectsListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(ProjectsListExample))]
    public async Task<IActionResult> Action(CancellationToken cancellationToken = default)
    {
        var userIdStr = User.Claims.FirstOrDefault(c => c.Type == Claims.UserId)?.Value;
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
        {
            logger.LogWarning("Unauthorized request: missing or invalid UserId claim");
            return Unauthorized(new Errors.UserUnauthorized("User is not authorized"));
        }

        var userExists = await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists)
        {
            logger.LogWarning("User {UserId} not found", userId);
            return NotFound(new Errors.UserNotFound("User was not found"));
        }

        var items = await db.Projects
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new ProjectSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                BboxWkt = p.Bbox.AsText(),
                CreatedAt = p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        logger.LogInformation("Returned {Count} projects for user {UserId}", items.Count, userId);

        var dto = new ProjectsListDto {Projects = items};
        return Ok(dto);
    }
}