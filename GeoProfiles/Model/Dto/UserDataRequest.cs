using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Model.Dto;

public record UserDataRequest
{
    public string Username { get; init; } = null!;
    public string Email { get; init; } = null!;

    public string PasswordHash { get; init; } = null!;
}

public record UserDataRequestExample : IExamplesProvider<UserDataRequest>
{
    public UserDataRequest GetExamples()
    {
        return new UserDataRequest
        {
            Username = "sample-username",
            Email = "sample-email@gmail.com",
            PasswordHash = "$2b$12$e9Vl0r1bUsYHjvXgk6x5hOe6vLqB5MEP5QmrW0CscJofzlhKg/a0G"
        };
    }
}