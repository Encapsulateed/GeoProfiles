using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Users;

public record UserDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = null!;
    public string Email { get; init; } = null!;
}

public record UserDtoExample : IExamplesProvider<UserDto>
{
    public UserDto GetExamples()
    {
        return new UserDto
        {
            Id = Guid.Parse("06739fee-a7ba-41a9-961c-d16dbd2ba285"),
            Username = "sample-username",
            Email = "sample-email@gmail.com"
        };
    }
}