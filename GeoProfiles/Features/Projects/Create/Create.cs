using System.Net.Mime;
using FluentValidation;
using GeoProfiles.Application.Auth;
using GeoProfiles.Infrastructure;
using GeoProfiles.Infrastructure.Services;
using GeoProfiles.Model.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Projects.Create
{
    using static DataRestrictions.Project;

    public class Create(
        GeoProfilesContext db,
        IIsolineGeneratorService isolineGenerator,
        ILogger<Create> logger) : ControllerBase
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
            new Validator().ValidateAndThrow(request);

            var userIdStr = User.Claims.FirstOrDefault(c => c.Type == Claims.UserId)?.Value;
            if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new Errors.UserUnauthorized("User is not authorized"));

            if (!await db.Users.AsNoTracking()
                    .AnyAsync(u => u.Id == userId, cancellationToken))
                return NotFound(new Errors.UserNotFound("User was not found"));

            var bboxInput = new BoundingBox(-0.1, -0.1, 0.1, 0.1);

            var isolinePolygons = (await isolineGenerator.GenerateAsync(
                    bboxInput))
                .ToList();

            if (isolinePolygons.Count == 0)
            {
                logger.LogError("Isoline generator produced 0 features");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            var env = new Envelope();
            foreach (var poly in isolinePolygons)
            {
                env.ExpandToInclude(poly.EnvelopeInternal);
                poly.SRID = 4326;
            }

            var gf = isolinePolygons[0].Factory;
            var bboxGeomCandidate = gf.ToGeometry(env);
            var bboxGeom = (Polygon) (bboxGeomCandidate is Polygon p ? p : bboxGeomCandidate.Buffer(1e-6));
            bboxGeom.SRID = 4326;

            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var project = new Model.Projects
                {
                    UserId = userId,
                    Name = request.Name,
                    Bbox = bboxGeom
                };
                db.Projects.Add(project);
                await db.SaveChangesAsync(cancellationToken);

                db.Isolines.AddRange(
                    isolinePolygons.Select((poly, idx) => new Model.Isolines
                    {
                        ProjectId = project.Id,
                        Level = idx,
                        Geom = poly
                    }));
                await db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                var dto = new ProjectDto
                {
                    Id = project.Id,
                    Name = project.Name,
                    BboxWkt = project.Bbox.AsText(),
                    Isolines = isolinePolygons.Select((poly, idx) => new IsolineDto
                    {
                        Level = idx,
                        GeomWkt = poly.AsText()
                    }).ToList()
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
            public Validator()
            {
                RuleFor(x => x.Name)
                    .NotEmpty()
                    .Length(NameMinLength, NameMaxLength);
            }
        }
    }
}