using GeoProfiles.Features.Projects.Create;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Projects.List;

public record ProjectsListDto
{
    public IList<ProjectSummaryDto> Projects { get; init; } = new List<ProjectSummaryDto>();
}

internal sealed class ProjectsListExample : IExamplesProvider<ProjectsListDto>
{
    public ProjectsListDto GetExamples() => new()
    {
        Projects = new List<ProjectSummaryDto>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Amsterdam centre",
                BboxWkt = "POLYGON((4.889 -52.373,4.899 -52.373,4.899 -52.363,4.889 -52.363,4.889 -52.373))",
                CreatedAt = DateTimeOffset.Parse("2025-05-20T14:30:00Z")
            },
            new()
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Berlin Mitte",
                BboxWkt = "POLYGON((13.38 52.51,13.4 52.51,13.4 52.5,13.38 52.5,13.38 52.51))",
                CreatedAt = DateTimeOffset.Parse("2025-05-22T09:15:00Z")
            }
        }
    };
}