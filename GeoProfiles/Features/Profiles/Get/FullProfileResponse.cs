using System.Text.Json.Serialization;
using GeoProfiles.Services;
using Swashbuckle.AspNetCore.Filters; // ProfilePoint (Distance, Elevation, IsOnIsoline)

namespace GeoProfiles.Features.Profiles.Get;

public record FullProfileResponse
{
    [JsonPropertyName("profileId")]  public Guid               ProfileId   { get; init; }
    [JsonPropertyName("start")]      public double[]           Start       { get; init; } = null!;
    [JsonPropertyName("end")]        public double[]           End         { get; init; } = null!;
    [JsonPropertyName("length_m")]   public decimal            LengthM     { get; init; }
    [JsonPropertyName("created_at")] public DateTime           CreatedAt   { get; init; }

    /// <summary>Полный сглаженный профиль (≈ 400 точек).</summary>
    [JsonPropertyName("points")]
    public IList<ProfilePoint> Points { get; init; } = null!;

    /// <summary>Подмножество точек, лежащих на изолиниях.</summary>
    [JsonPropertyName("mainPoints")]
    public IList<ProfilePoint> MainPoints { get; init; } = null!;
}

public class ProfileResponseExample : IExamplesProvider<FullProfileResponse>
{
    public FullProfileResponse GetExamples() => new()
    {
        ProfileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Start     = [30.100, 59.900],
        End       = [30.500, 60.200],
        LengthM   = 500.0m,
        CreatedAt = DateTime.Parse("2025-05-20T10:00:00Z"),

        Points =
        [
            new(   0.0, 100.0, true),
            new( 125.0, 110.0, false),
            new( 250.0, 120.0, true),
            new( 375.0, 115.0, false),
            new( 500.0, 105.0, true)
        ],
        MainPoints =
        [
            new(0.0,   100.0, true),
            new(250.0, 120.0, true),
            new(500.0, 105.0, true)
        ]
    };
}