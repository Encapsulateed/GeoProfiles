using System.Text.Json.Serialization;
using GeoProfiles.Infrastructure.Services;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Profiles.Create;

public record ProfileResponse
{
    [JsonPropertyName("profileId")] public Guid ProfileId { get; init; }

    [JsonPropertyName("length_m")] public decimal LengthM { get; init; }

    public List<ProfilePoint> Points { get; set; } = null!;
}

public class ProfileResponseExample : IExamplesProvider<ProfileResponse>
{
    public ProfileResponse GetExamples() =>
        new()
        {
            ProfileId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            LengthM = 1234.56m,
            Points =
            [
                new ProfilePoint(0.0, 10.5),
                new ProfilePoint(100.0, 15.2),
                new ProfilePoint(200.0, 20.1),
                new ProfilePoint(300.0, 18.7),
                new ProfilePoint(400.0, 22.4),
                new ProfilePoint(500.0, 25.0),
                new ProfilePoint(600.0, 23.8),
                new ProfilePoint(700.0, 27.5),
                new ProfilePoint(800.0, 30.0)
            ]
        };
}