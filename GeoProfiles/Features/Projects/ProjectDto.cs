using GeoProfiles.Model.Dto;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Projects;

public record ProjectDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = null!;

    public string BboxWkt { get; init; } = null!;

    public IReadOnlyList<IsolineDto> Isolines { get; init; } = null!;
}

public sealed class ProjectDtoExample : IExamplesProvider<ProjectDto>
{
    public ProjectDto GetExamples()
    {
        return new ProjectDto
        {
            Id = Guid.Parse("06739fee-a7ba-41a9-961c-d16dbd2ba285"),
            Name = "Demo project",
            BboxWkt = "POLYGON((10 10, 10 20, 20 20, 20 10, 10 10))",
            Isolines = Array.Empty<IsolineDto>()
        };
    }
}