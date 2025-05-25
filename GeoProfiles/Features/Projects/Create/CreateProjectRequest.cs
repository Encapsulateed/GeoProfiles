using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Projects.Create;

public record CreateProjectRequest
{
    public string Name { get; init; } = null!;
}

public record ProjectCreateRequestExample : IExamplesProvider<CreateProjectRequest>
{
    public CreateProjectRequest GetExamples()
    {
        return new CreateProjectRequest
        {
            Name = "Project 1"
        };
    }
}