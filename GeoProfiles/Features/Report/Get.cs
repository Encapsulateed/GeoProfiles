// GeoProfiles/Features/Reports/Get/Get.cs
using System.Net.Mime;
using FluentValidation;
using GeoProfiles.Application.Auth;
using GeoProfiles.Features.Profiles.Get;
using GeoProfiles.Features.Projects.Create;
using GeoProfiles.Infrastructure;
using GeoProfiles.Model.Dto;
using GeoProfiles.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Reports.Get;

public class Get(
    GeoProfilesContext          db,
    ITerrainProfileService      profileService,
    ILogger<Get>                logger) : ControllerBase
{
    [HttpGet("api/v1/projects/{projectId:guid}/profiles/{profileId:guid}/report")]
    [Authorize]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(ReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse),   StatusCodes.Status404NotFound)]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(ReportResponseExample))]
    public async Task<IActionResult> Action(
        [FromRoute] ReportRequest request,
        CancellationToken         cancellationToken = default)
    {
        new ReportRequestValidator().ValidateAndThrow(request);

        /* ── auth ─*/
        var userIdStr = User.Claims.FirstOrDefault(c => c.Type == Claims.UserId)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized(new Errors.UserUnauthorized("Unauthorized"));

        /* ── project ─*/
        var project = await db.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == request.projectId && p.UserId == userId, cancellationToken);
        if (project is null)
            return NotFound(new Errors.ProjectNotFound("Project not found"));

        /* ── profile entity ─*/
        var profileEnt = await db.TerrainProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(tp => tp.Id == request.profileId && tp.ProjectId == project.Id, cancellationToken);
        if (profileEnt is null)
            return NotFound(new Errors.ResourceNotFound("Profile not found"));

        /* ── isolines (первая 5k) ─*/
        var isolines = await db.ContourLines.AsNoTracking()
            .Where(c => c.Geom != null)
            .OrderBy(c => c.Fid)
            .Take(1000)
            .Select(c => new IsolineDto(c.Level ?? double.NaN, c.Geom!.AsText()))
            .ToListAsync(cancellationToken);

        /* ── points from DB → memory, затем в ProfilePoint ─*/
        var rawPts = await db.TerrainProfilePoints.AsNoTracking()
            .Where(p => p.ProfileId == profileEnt.Id)
            .OrderBy(p => p.Seq)
            .ToListAsync(cancellationToken);

        var points = rawPts
            .Select(p => new ProfilePoint((double)p.DistM, (double)p.ElevM, false))
            .ToList();

        /* mainPoints: берём только метки IsOnIsoline == true,
           если в БД такого флага нет – маркируем здесь по совпадению с уровнями */
        const double lvlEps = 1e-3;
        foreach (var pt in points)
        {
            if (isolines.Any(i => Math.Abs(i.Level - pt.Elevation) < lvlEps))
            {
                var idx = points.IndexOf(pt);
                points[idx] = pt with { IsOnIsoline = true };
            }
        }
        var mainPts = points.Where(p => p.IsOnIsoline).ToList();

        /* ── build DTO ─*/
        var profileDto = new FullProfileResponse
        {
            ProfileId  = profileEnt.Id,
            Start      = [profileEnt.StartPt.X, profileEnt.StartPt.Y],
            End        = [profileEnt.EndPt.X,   profileEnt.EndPt.Y],
            LengthM    = profileEnt.LengthM,
            CreatedAt  = profileEnt.CreatedAt,
            Points     = points,
            MainPoints = mainPts
        };

        var resp = new ReportResponse
        {
            ProjectId = project.Id,
            Name      = project.Name,
            BboxWkt   = project.Bbox.AsText(),
            Isolines  = isolines,
            Profile   = profileDto
        };

        return Ok(resp);
    }
}
