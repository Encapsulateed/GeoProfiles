using System.Security.Claims;
using FluentValidation;
using GeoProfiles.Application.Auth;
using GeoProfiles.Infrastructure;
using GeoProfiles.Infrastructure.Examples;
using GeoProfiles.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Profiles.Get;

public class Get(GeoProfilesContext db, ILogger<Get> logger) : ControllerBase
{
    [HttpGet("api/v1/{projectId:guid}/profile/{profileId:guid}")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(FullProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(ProfileResponseExample))]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(BadRequestExample))]
    public async Task<IActionResult> Action(
        [FromRoute] ProfileGetRequest request,
        CancellationToken cancellationToken = default)
    {
        new Validator().ValidateAndThrow(request);

        var sub = User.FindFirstValue(Claims.UserId);

        if (!Guid.TryParse(sub, out var userId))
        {
            logger.LogInformation("User is not authorized");
            return Unauthorized(new Errors.UserUnauthorized("User is not authorized"));
        }

        var project = await db.Projects
            .AsNoTracking()
            .Where(p => p.Id == request.ProjectId && p.UserId == userId)
            .SingleOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            logger.LogInformation("Project not found or not owned by user");
            return NotFound(new Errors.ProjectNotFound("Project not found"));
        }

        var profile = await db.TerrainProfiles
            .AsNoTracking()
            .Where(tp => tp.Id == request.ProfileId && tp.ProjectId == request.ProjectId)
            .SingleOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            logger.LogInformation("Profile not found or does not belong to project");
            return NotFound(new Errors.ResourceNotFound("Profile not found"));
        }

        var points = await db.TerrainProfilePoints
            .AsNoTracking()
            .Where(p => p.ProfileId == request.ProfileId)
            .OrderBy(p => p.Seq)
            .Select(p => new ProfilePoint(
                (double) p.DistM,
                (double) p.ElevM))
            .ToListAsync(cancellationToken);

        var response = new FullProfileResponse
        {
            ProfileId = profile.Id,
            Start = [profile.StartPt.X, profile.StartPt.Y],
            End = [profile.EndPt.X, profile.EndPt.Y],
            LengthM = profile.LengthM,
            CreatedAt = profile.CreatedAt,
            Points = points
        };

        return Ok(response);
    }

    private class Validator : AbstractValidator<ProfileGetRequest>
    {
        public Validator()
        {
            RuleFor(x => x.ProjectId)
                .NotEmpty()
                .WithMessage("'projectId' must be provided and be a valid GUID");

            RuleFor(x => x.ProfileId)
                .NotEmpty()
                .WithMessage("'profileId' must be provided and be a valid GUID");
        }
    }
}