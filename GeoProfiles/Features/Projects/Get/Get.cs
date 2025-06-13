using System.Net.Mime;
using GeoProfiles.Application.Auth;
using GeoProfiles.Features.Projects.Create;
using GeoProfiles.Infrastructure;
using GeoProfiles.Model.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Projects.Get;

public class Get(
    GeoProfilesContext db,
    ILogger<Get> logger) : ControllerBase
{
    [HttpGet("api/v1/projects/{id:guid}")]
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

        // ── проверка авторизации ──────────────────────────────────────────────────
        var userIdStr = User.Claims.FirstOrDefault(c => c.Type == Claims.UserId)?.Value;
        if (userIdStr is null || !Guid.TryParse(userIdStr, out var userId))
        {
            logger.LogInformation("User is not authorized");
            return Unauthorized(new Errors.UserUnauthorized("User is not authorized"));
        }

        // ── достаём сам проект без навигации изолиний ─────────────────────────────
        var project = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);

        if (project is null)
        {
            logger.LogInformation("Project was not found");
            return NotFound(new Errors.ProjectNotFound("Project was not found"));
        }

        // ── берём «общие» изолинии (первые 5000) ──────────────────────────────────
        var isolines = await db.ContourLines
            .AsNoTracking()
            .Where(cl => cl.Geom != null)
            .OrderBy(cl => cl.Fid)          // жёсткое бизнес-правило MVP
            .Take(1000)
            .Select(cl => new              // сразу фиксируем SRID, чтобы фронту было проще
            {
                LineString = cl.Geom!,     // гарантированно non-null после Where
                cl.Level
            })
            .ToListAsync(cancellationToken);

        if (isolines.Count == 0)
        {
            logger.LogError("No isolines found in ContourLines table");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        foreach (var l in isolines)
            l.LineString.SRID = 4326;       // на всякий случай

        // ── собираем DTO ──────────────────────────────────────────────────────────
        var dto = new ProjectDto
        {
            Id       = project.Id,
            Name     = project.Name,
            BboxWkt  = project.Bbox.AsText(),
            Isolines = isolines
                .OrderBy(i => i.Level)     // для фронта красивые слои «снизу-вверх»
                .Select(i => new IsolineDto(
                    Level:   i.Level ?? double.NaN,
                    GeomWkt: i.LineString.AsText()))
                .ToList()
        };

        logger.LogInformation("Returned project {ProjectId}", project.Id);
        return Ok(dto);
    }
}
