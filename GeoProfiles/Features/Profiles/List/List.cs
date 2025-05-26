using System.Net.Mime;
using System.Security.Claims;
using FluentValidation;
using GeoProfiles.Application.Auth;
using GeoProfiles.Infrastructure;
using GeoProfiles.Infrastructure.Examples;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Profiles.List;

public class List(GeoProfilesContext db, ILogger<List> logger) : ControllerBase
{
    [HttpGet("api/v1/{projectId:guid}/list")]
    [Authorize]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(ProfileList), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(ProfileListExample))]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(BadRequestExample))]
    public async Task<IActionResult> Action(
        [FromRoute] Guid projectId,
        CancellationToken cancellationToken = default)
    {
        new Validator().ValidateAndThrow(projectId);

        var sub = User.FindFirstValue(Claims.UserId);

        if (!Guid.TryParse(sub, out var userId))
        {
            logger.LogInformation("User is not authorized");
            return Unauthorized(new Errors.UserUnauthorized("User is not authorized"));
        }

        var project = await db.Projects
            .AsNoTracking()
            .Where(x => x.Id == projectId && x.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            logger.LogInformation("Project not found or not owned by user");
            return NotFound(new Errors.ProjectNotFound("Project not found or not owned by user"));
        }

        var items = await db.TerrainProfiles
            .AsNoTracking()
            .Where(tp => tp.ProjectId == projectId)
            .OrderBy(tp => tp.CreatedAt)
            .Select(tp => new ProfileListItem
            {
                Id = tp.Id,
                Start = new double[] {tp.StartPt.X, tp.StartPt.Y},
                End = new double[] {tp.EndPt.X, tp.EndPt.Y},
                LengthM = tp.LengthM,
                CreatedAt = tp.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new ProfileList {Items = items});
    }


    private class Validator : AbstractValidator<Guid>
    {
        public Validator()
        {
            RuleFor(x => x).NotEmpty();
        }
    }
}