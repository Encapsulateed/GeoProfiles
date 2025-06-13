using System.Net.Mime;
using FluentValidation;
using GeoProfiles.Application.Auth;
using GeoProfiles.Infrastructure;
using GeoProfiles.Model;
using GeoProfiles.Model.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Projects.Create;

using static DataRestrictions.Project;

public class Create(
    GeoProfilesContext db,
    ILogger<Create> logger) : ControllerBase
{
    [HttpPost("api/v1/projects")]
    [Authorize]
    [Produces(MediaTypeNames.Application.Json)]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse),  StatusCodes.Status400BadRequest)]
    [SwaggerRequestExample(typeof(CreateProjectRequest), typeof(ProjectCreateRequestExample))]
    [SwaggerResponseExample(StatusCodes.Status201Created, typeof(ProjectDtoExample))]
    public async Task<IActionResult> Action(
        [FromBody] CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating project");
        new Validator().ValidateAndThrow(request);

        var userIdStr = User.Claims.FirstOrDefault(c => c.Type == Claims.UserId)?.Value;
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized(new Errors.UserUnauthorized("User is not authorized"));

        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken))
            return NotFound(new Errors.UserNotFound("User was not found"));

        var sourceIsolines = await db.ContourLines
            .AsNoTracking()
            .OrderBy(cl => cl.Fid)
            .Take(1000)
            .Where(cl => cl.Geom != null)
            .Select(cl => new { LineString = cl.Geom!, cl.Level })
            .ToListAsync(cancellationToken);

        if (sourceIsolines.Count == 0)
        {
            logger.LogError("No isolines found in ContourLines table");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        var env = new Envelope();
        foreach (var line in sourceIsolines)
        {
            line.LineString.SRID = 4326;
            env.ExpandToInclude(line.LineString.EnvelopeInternal);
        }

        var gf        = sourceIsolines[0].LineString.Factory;
        var bboxGeom  = gf.ToGeometry(env) switch
        {
            Polygon p => p,
            Geometry g => (Polygon)g.Buffer(1e-6)
        };
        bboxGeom.SRID = 4326;

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var project = new Model.Projects
            {
                UserId = userId,
                Name   = request.Name,
                Bbox   = bboxGeom
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            var dto = new ProjectDto
            {
                Id      = project.Id,
                Name    = project.Name,
                BboxWkt = bboxGeom.AsText(),
                Isolines = sourceIsolines.Select(l =>
                    new IsolineDto(
                        Level: l.Level ?? double.NaN,
                        GeomWkt: l.LineString.AsText()))
                    .ToList()
            };

            logger.LogInformation("Successfully created project {ProjectId}", project.Id);
            return StatusCode(StatusCodes.Status201Created, dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create project");
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private sealed class Validator : AbstractValidator<CreateProjectRequest>
    {
        public Validator() =>
            RuleFor(x => x.Name)
                .NotEmpty()
                .Length(NameMinLength, NameMaxLength);
    }
}
