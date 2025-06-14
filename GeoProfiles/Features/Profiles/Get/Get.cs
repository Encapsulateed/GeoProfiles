using System.Security.Claims;
using FluentValidation;
using GeoProfiles.Application.Auth;
using GeoProfiles.Infrastructure;
using GeoProfiles.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Profiles.Get;

public class Get(GeoProfilesContext db, ILogger<Get> logger) : ControllerBase
{
    [HttpGet("api/v1/{projectId:guid}/profile/{profileId:guid}")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(FullProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse),       StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse),       StatusCodes.Status404NotFound)]
    [SwaggerResponseExample(StatusCodes.Status200OK,   typeof(ProfileResponseExample))]
    public async Task<IActionResult> Action(
        [FromRoute] ProfileGetRequest request,
        CancellationToken             cancellationToken = default)
    {
        new ProfileGetRequestValidator().ValidateAndThrow(request);

        var sub = User.FindFirstValue(Claims.UserId);
        if (!Guid.TryParse(sub, out var userId))
        {
            logger.LogInformation("User is not authorized");
            return Unauthorized(new Errors.UserUnauthorized("User is not authorized"));
        }

        var project = await db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(
                p => p.Id == request.ProjectId && p.UserId == userId,
                cancellationToken);

        if (project is null)
        {
            logger.LogInformation("Project not found or not owned by user");
            return NotFound(new Errors.ProjectNotFound("Project not found"));
        }

        var profile = await db.TerrainProfiles.AsNoTracking()
            .SingleOrDefaultAsync(
                tp => tp.Id == request.ProfileId && tp.ProjectId == request.ProjectId,
                cancellationToken);

        if (profile is null)
        {
            logger.LogInformation("Profile not found or does not belong to project");
            return NotFound(new Errors.ResourceNotFound("Profile not found"));
        }

        var rawPts = await db.TerrainProfilePoints.AsNoTracking()
            .Where(p => p.ProfileId == request.ProfileId)
            .OrderBy(p => p.Seq)
            .Select(p => new { p.DistM, p.ElevM })
            .ToListAsync(cancellationToken);

        var startLon = profile.StartPt.X;
        var startLat = profile.StartPt.Y;
        var endLon   = profile.EndPt.X;
        var endLat   = profile.EndPt.Y;
        var lenTotal = (double)profile.LengthM;

        var points = new List<ProfilePoint>(rawPts.Count);
        foreach (var tpp in rawPts)
        {
            var dist = (double)tpp.DistM;
            double t = dist / lenTotal;
            var lon = startLon + t * (endLon - startLon);
            var lat = startLat + t * (endLat - startLat);
            var ptW = new Point(lon, lat) { SRID = 4326 };

            bool isOnIso = await db.ContourLines.AsNoTracking()
                .AnyAsync(cl =>
                    cl.Geom != null &&
                    cl.Geom.IsWithinDistance(ptW, 1e-5),
                    cancellationToken);

            points.Add(new ProfilePoint(dist, (double)tpp.ElevM, isOnIso));
        }

        var response = new FullProfileResponse
        {
            ProfileId  = profile.Id,
            Start      = [profile.StartPt.X, profile.StartPt.Y],
            End        = [profile.EndPt.X,   profile.EndPt.Y],
            LengthM    = profile.LengthM,
            CreatedAt  = profile.CreatedAt,
            Points     = points,
            MainPoints = points.Where(p => p.IsOnIsoline).ToList()
        };

        return Ok(response);
    }

    private sealed class ProfileGetRequestValidator : AbstractValidator<ProfileGetRequest>
    {
        public ProfileGetRequestValidator()
        {
            RuleFor(x => x.ProjectId)
                .NotEmpty()
                .WithMessage("'projectId' must be a valid GUID");

            RuleFor(x => x.ProfileId)
                .NotEmpty()
                .WithMessage("'profileId' must be a valid GUID");
        }
    }
}
