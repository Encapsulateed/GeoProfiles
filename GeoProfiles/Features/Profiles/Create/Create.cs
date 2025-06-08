using System.Net.Mime;
using FluentValidation;
using GeoProfiles.Application.Auth;
using GeoProfiles.Infrastructure;
using GeoProfiles.Infrastructure.Examples;
using GeoProfiles.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Profiles.Create;

public class Create(
    GeoProfilesContext db,
    ITerrainProfileService terrainProfileService,
    ILogger<Create> logger) : ControllerBase
{
    [HttpPost("api/v1/{projectId:guid}/profile")]
    [Authorize]
    [Produces(MediaTypeNames.Application.Json)]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [SwaggerRequestExample(typeof(ProfileRequest), typeof(ProfileRequestExample))]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(ProfileResponseExample))]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(BadRequestExample))]
    public async Task<IActionResult> Action(
        [FromRoute] Guid projectId,
        [FromBody] ProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating profile for project {ProjectId}", projectId);
        new Validator().ValidateAndThrow(request);

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
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken: cancellationToken);

        if (user is null)
        {
            logger.LogInformation("User not found");

            return NotFound(new Errors.UserNotFound("User not found"));
        }

        var project = await db.Projects
            .Where(p => p.UserId == userId && p.Id == projectId)
            .SingleOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            logger.LogInformation("Project not found");

            return NotFound(new Errors.ProjectNotFound("Project not found"));
        }

        var start = new Point(request.Start[0], request.Start[1]) {SRID = 4326};
        var end = new Point(request.End[0], request.End[1]) {SRID = 4326};

        var profile = await terrainProfileService.BuildProfileAsync(start, end, projectId, ct: cancellationToken);

        var response = new ProfileResponse
        {
            ProfileId = profile.Id,
            LengthM = await db.TerrainProfiles
                .Where(tp => tp.Id == profile.Id)
                .Select(tp => tp.LengthM)
                .SingleAsync(cancellationToken)
        };

        var pts = await db.TerrainProfilePoints
            .AsNoTracking()
            .Where(tpp => tpp.ProfileId == profile.Id)
            .OrderBy(tpp => tpp.Seq)
            .Select(tpp => new ProfilePoint(
                (double) tpp.DistM,
                (double) tpp.ElevM))
            .ToListAsync(cancellationToken);

        response.Points = pts;

        return StatusCode(StatusCodes.Status201Created, response);
    }

    private class Validator : AbstractValidator<ProfileRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Start)
                .NotNull()
                .WithMessage("'start' must be provided");

            RuleFor(x => x.Start)
                .Must(a => a.Length == 2)
                .WithMessage("'start' must contain exactly two elements [lon, lat]")
                .When(x => x.Start != null);

            RuleFor(x => x.Start)
                .Must(a => a[0] >= -180 && a[0] <= 180)
                .WithMessage("Longitude in 'start' must be between -180 and 180")
                .When(x => x.Start != null && x.Start.Length == 2);

            RuleFor(x => x.Start)
                .Must(a => a[1] >= -90 && a[1] <= 90)
                .WithMessage("Latitude in 'start' must be between -90 and 90")
                .When(x => x.Start != null && x.Start.Length == 2);

            RuleFor(x => x.End)
                .NotNull()
                .WithMessage("'end' must be provided");

            RuleFor(x => x.End)
                .Must(a => a.Length == 2)
                .WithMessage("'end' must contain exactly two elements [lon, lat]")
                .When(x => x.End != null);

            RuleFor(x => x.End)
                .Must(a => a[0] >= -180 && a[0] <= 180)
                .WithMessage("Longitude in 'end' must be between -180 and 180")
                .When(x => x.End != null && x.End.Length == 2);

            RuleFor(x => x.End)
                .Must(a => a[1] >= -90 && a[1] <= 90)
                .WithMessage("Latitude in 'end' must be between -90 and 90")
                .When(x => x.End != null && x.End.Length == 2);

            RuleFor(x => x)
                .Must(x =>
                    x.Start != null && x.End != null &&
                    (x.Start[0] != x.End[0] || x.Start[1] != x.End[1])
                )
                .WithMessage("'start' and 'end' must be different points")
                .When(x =>
                    x.Start != null && x.End != null &&
                    x.Start.Length == 2 && x.End.Length == 2
                );
        }
    }
}