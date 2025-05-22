using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.JWT;

public record RefreshDto
{
    public string Token { get; init; } = null!;

    public string RefreshToken { get; init; } = null!;
}

public class RefreshDtoExample : IExamplesProvider<RefreshDto>
{
    public RefreshDto GetExamples()
    {
        return new RefreshDto
        {
            Token = "eyJhbG\u0063iOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
            RefreshToken = "d9\u003428888-122b-11e1-b85c-61cd3cbb3210"
        };
    }
}
