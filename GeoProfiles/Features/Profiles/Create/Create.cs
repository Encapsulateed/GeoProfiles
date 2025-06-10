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
    GeoProfilesContext          db,
    ITerrainProfileService      terrainProfileService,
    ILogger<Create>             logger) : ControllerBase
{
    [HttpPost("api/v1/{projectId:guid}/profile")]
    [Authorize]
    [Produces(MediaTypeNames.Application.Json)]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse),     StatusCodes.Status400BadRequest)]
    [SwaggerRequestExample (typeof(ProfileRequest),          typeof(ProfileRequestExample))]
    [SwaggerResponseExample(StatusCodes.Status201Created,    typeof(ProfileResponseExample))]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(BadRequestExample))]
    public async Task<IActionResult> Action(
        [FromRoute] Guid projectId,
        [FromBody]  ProfileRequest request,
        CancellationToken          cancellationToken = default)
    {
        logger.LogInformation("Creating profile for project {ProjectId}", projectId);
        new Validator().ValidateAndThrow(request);

        // ── авторизация ─────────────────────────────────────────────────────────
        var userIdStr = User.Claims.FirstOrDefault(c => c.Type == Claims.UserId)?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
        {
            logger.LogInformation("User is not authorized");
            return Unauthorized(new Errors.UserUnauthorized("Unauthorized"));
        }

        var userExists = await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists)
        {
            logger.LogInformation("User not found");
            return NotFound(new Errors.UserNotFound("User not found"));
        }

        // ── проверка проекта ────────────────────────────────────────────────────
        var project = await db.Projects
            .Where(p => p.UserId == userId && p.Id == projectId)
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null)
        {
            logger.LogInformation("Project not found");
            return NotFound(new Errors.ProjectNotFound("Project not found"));
        }

        // ── построение профиля ──────────────────────────────────────────────────
        var start = new Point(request.Start[0], request.Start[1]) { SRID = 4326 };
        var end   = new Point(request.End[0],   request.End[1])   { SRID = 4326 };

        var profile = await terrainProfileService
            .BuildProfileAsync(start, end, projectId, ct: cancellationToken);

        // ── формируем DTO ───────────────────────────────────────────────────────
        var response = new ProfileResponse
        {
            ProfileId  = profile.Id,
            LengthM    = profile.LengthM,
            Points     = profile.Points.ToList(),
            MainPoints = profile.Points
                              .Where(p => p.IsOnIsoline)
                              .ToList()
        };

        logger.LogInformation("Profile {ProfileId} created", profile.Id);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    // ────────────────────────────────────────────────────────────────────────────
    private class Validator : AbstractValidator<ProfileRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Start)
                .NotNull().WithMessage("'start' must be provided")
                .Must(a => a.Length == 2)
                .WithMessage("'start' must contain exactly two elements [lon, lat]")
                .Must(a => a[0] is >= -180 and <= 180)
                .WithMessage("Longitude in 'start' must be between -180 and 180")
                .Must(a => a[1] is >= -90 and <= 90)
                .WithMessage("Latitude in 'start' must be between -90 and 90");

            RuleFor(x => x.End)
                .NotNull().WithMessage("'end' must be provided")
                .Must(a => a.Length == 2)
                .WithMessage("'end' must contain exactly two elements [lon, lat]")
                .Must(a => a[0] is >= -180 and <= 180)
                .WithMessage("Longitude in 'end' must be between -180 and 180")
                .Must(a => a[1] is >= -90 and <= 90)
                .WithMessage("Latitude in 'end' must be between -90 and 90");

            RuleFor(x => x)
                .Must(x => x.Start[0] != x.End[0] || x.Start[1] != x.End[1])
                .WithMessage("'start' and 'end' must be different points");
        }
    }
}
