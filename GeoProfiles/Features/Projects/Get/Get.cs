using System.Net.Mime;
using GeoProfiles.Application.Auth;
using GeoProfiles.Features.Projects.Create;
using GeoProfiles.Infrastructure;
using GeoProfiles.Model.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Projects.Get;

public class Get(
    GeoProfilesContext db,
    ILogger<Get> logger) : ControllerBase
{
    [HttpGet("api/v1/project/{id:guid}")]
    [Authorize]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(ProjectDtoExample))]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(ErrorResponse))]
    public async Task<IActionResult> Action(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting project {ProjectId}", id);

        var userIdStr = User.Claims.FirstOrDefault(c => c.Type == Claims.UserId)?.Value;

        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
        {
            logger.LogInformation("User is not authorized");
            return Unauthorized(new Errors.UserUnauthorized("User is not authorized"));
        }

        var project = await db.Projects
            .Include(p => p.Isolines.OrderBy(i => i.Level))
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);

        if (project is null)
        {
            logger.LogInformation("Project was not found");
            return NotFound(new Errors.ProjectNotFound("Project was not found"));
        }

        var dto = new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            BboxWkt = project.Bbox.AsText(),
            Isolines = project.Isolines
                .Select(i => new IsolineDto
                {
                    Level = i.Level,
                    GeomWkt = i.Geom.AsText()
                })
                .ToList()
        };

        logger.LogInformation("Returned project {ProjectId}", project.Id);
        return Ok(dto);
    }
}