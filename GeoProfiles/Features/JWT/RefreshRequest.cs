using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.JWT;

public record RefreshRequest
{
    public string RefreshToken { get; init; } = null!;
}

public class RefreshRequestExample : IExamplesProvider<RefreshRequest>
{
    public RefreshRequest GetExamples()
    {
        return new RefreshRequest
        {
            RefreshToken = "f4710b-58cc-437\u0032-a567-0e02b2c3d479"
        };
    }
}