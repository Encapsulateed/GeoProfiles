using GeoProfiles.Model.Dto;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Projects.Create;


public record IsolineDto(double Level, string GeomWkt);

public record ProjectDto
{
    public Guid Id           { get; init; }
    public string Name       { get; init; } = null!;
    public string BboxWkt    { get; init; } = null!;
    public IReadOnlyList<IsolineDto> Isolines { get; init; } = null!;
}

public sealed class ProjectDtoExample : IExamplesProvider<ProjectDto>
{
    public ProjectDto GetExamples() => new()
    {
        Id      = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Name    = "Amsterdam Central Area",
        BboxWkt = "POLYGON ((4.889 -52.373, 4.899 -52.373, 4.899 -52.363, 4.889 -52.363, 4.889 -52.373))",
        Isolines =
        [
            new IsolineDto(0, "LINESTRING (…)"),
            new IsolineDto(1, "LINESTRING (…)")
        ]
    };
}
