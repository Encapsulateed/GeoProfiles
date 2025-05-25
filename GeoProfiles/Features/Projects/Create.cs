using System.Net.Mime;
using System.Security.Claims;
using FluentValidation;
using GeoProfiles.Application.Auth;
using GeoProfiles.Infrastructure;
using GeoProfiles.Infrastructure.Services;
using GeoProfiles.Model.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Projects;

using static DataRestrictions.Project;

public class Create(
    GeoProfilesContext db,
    IIsolineGeneratorService isolineGenerator,
    ILogger<Create> logger)
    : ControllerBase
{
    [HttpPost("api/v1/projects")]
    [Authorize]
    [Produces(MediaTypeNames.Application.Json)]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [SwaggerRequestExample(typeof(CreateProjectRequest), typeof(ProjectCreateRequestExample))]
    [SwaggerResponseExample(StatusCodes.Status201Created, typeof(ProjectDtoExample))]
    public async Task<IActionResult> Action(
        [FromBody] CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating project");

        // ReSharper disable once MethodHasAsyncOverloadWithCancellation
        new Validator().ValidateAndThrow(request);

        var userIdClaim = User.Claims
            .FirstOrDefault(c => c.Type == Claims.UserId)?.Value;

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            logger.LogInformation("User is not authorized to create a project");

            return Unauthorized(new Errors.UserUnauthorized("User is not authorized to create a project"));
        }

        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == userId, cancellationToken: cancellationToken);

        if (user is null)
        {
            logger.LogInformation("User was not found");

            return NotFound(new Errors.UserNotFound("User was not found"));
        }

        var (bbox, isolines) = await isolineGenerator.GenerateAsync(5, cancellationToken);


        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var projectEntity = new Model.Projects
        {
            UserId = user.Id,
            Name = request.Name,
            Bbox = bbox
        };

        db.Add(projectEntity);
        await db.SaveChangesAsync(cancellationToken);

        if (isolines.Count > 0)
        {
            db.Isolines.AddRange(isolines.Select(i => new Model.Isolines
            {
                ProjectId = projectEntity.Id,
                Level = i.Level,
                Geom = i.Geometry
            }));
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        var dto = new ProjectDto
        {
            Id = projectEntity.Id,
            Name = projectEntity.Name,
            BboxWkt = projectEntity.Bbox.AsText(),
            Isolines = isolines.Select(i => new IsolineDto
                {
                    Level = i.Level,
                    GeomWkt = i.Geometry.AsText()
                })
                .ToList()
        };

        logger.LogInformation("Successfully created project");

        return StatusCode(StatusCodes.Status201Created, dto);
    }

    private class Validator : AbstractValidator<CreateProjectRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .Length(NameMinLength, NameMaxLength);
        }
    }
}