using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Auth.Register;

public record RegisterRequest
{
    public string Username { get; init; } = null!;
    public string Email { get; init; } = null!;

    public string PasswordHash { get; init; } = null!;
}

public record RegisterRequestExample : IExamplesProvider<RegisterRequest>
{
    public RegisterRequest GetExamples()
    {
        return new RegisterRequest
        {
            Username = "sample-username",
            Email = "sample-email@gmail.com",
            PasswordHash = "$2b$12$e9Vl0r1bUsYHjvXgk6x5hOe6vLqB5MEP5QmrW0CscJofzlhKg/a0G"
        };
    }
}