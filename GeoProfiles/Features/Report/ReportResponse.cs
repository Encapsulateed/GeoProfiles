// GeoProfiles/Features/Reports/Get/ReportDto.cs
using System.Text.Json.Serialization;
using GeoProfiles.Features.Profiles.Get;
using GeoProfiles.Features.Projects.Create; // FullProfileResponse
using GeoProfiles.Model.Dto;              // IsolineDto
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Reports.Get;

/// <summary>Ответ /report – для генерации PDF на фронте.</summary>
public record ReportResponse
{
    [JsonPropertyName("projectId")]  public Guid   ProjectId { get; init; }
    [JsonPropertyName("name")]       public string Name      { get; init; } = null!;
    [JsonPropertyName("bbox_wkt")]   public string BboxWkt   { get; init; } = null!;

    /// <summary>До 5 000 изолиний (как в ручке CreateProject).</summary>
    public List<IsolineDto> Isolines { get; init; } = null!;

    /// <summary>Полный профиль с точками.</summary>
    public FullProfileResponse Profile { get; init; } = null!;
}

/* ---------- Swagger example ---------- */
public class ReportResponseExample : IExamplesProvider<ReportResponse>
{
    public ReportResponse GetExamples() => new()
    {
        ProjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Name      = "North Ridge",
        BboxWkt   = "POLYGON((30 60, 31 60, 31 61, 30 61, 30 60))",
        Isolines  =
        [
            new IsolineDto(650, "LINESTRING(30 60, 30.5 60.5)"),
            new IsolineDto(660, "LINESTRING(30 60.1, 30.6 60.6)")
        ],
        Profile = new FullProfileResponse
        {
            ProfileId  = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Start      = [30.100, 60.000],
            End        = [30.400, 60.400],
            LengthM    = 1234m,
            CreatedAt  = DateTime.Parse("2025-06-01T10:00:00Z"),
            Points =
            [
                new(0,    640, true),
                new(200,  650, false),
                new(400,  670, true)
            ],
            MainPoints =
            [
                new(0,   640, true),
                new(400, 670, true)
            ]
        }
    };
}
