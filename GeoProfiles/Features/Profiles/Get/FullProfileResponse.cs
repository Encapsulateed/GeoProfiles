using System.Text.Json.Serialization;
using GeoProfiles.Infrastructure.Services;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Profiles.Get;

public record FullProfileResponse
{
    [JsonPropertyName("profileId")] public Guid ProfileId { get; init; }

    [JsonPropertyName("start")] public double[] Start { get; init; } = null!;

    [JsonPropertyName("end")] public double[] End { get; init; } = null!;

    [JsonPropertyName("length_m")] public decimal LengthM { get; init; }

    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }

    [JsonPropertyName("points")] public IList<ProfilePoint> Points { get; init; } = null!;
}

public class ProfileResponseExample : IExamplesProvider<FullProfileResponse>
{
    public FullProfileResponse GetExamples() => new()
    {
        ProfileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Start = [30.100, 59.900],
        End = [30.500, 60.200],
        LengthM = 500.0m,
        CreatedAt = DateTime.Parse("2025-05-20T10:00:00Z"),
        Points = new List<ProfilePoint>
        {
            new(0.0, 100.0),
            new(125.0, 110.0),
            new(250.0, 120.0),
            new(375.0, 115.0),
            new(500.0, 105.0)
        }
    };
}