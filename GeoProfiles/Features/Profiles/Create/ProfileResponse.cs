using System.Text.Json.Serialization;
using GeoProfiles.Services;
using Swashbuckle.AspNetCore.Filters; // рекорд ProfilePoint (с флагом IsOnIsoline)

namespace GeoProfiles.Features.Profiles.Create;

/// <summary>
/// Ответ на POST /api/v1/{projectId}/profile
/// </summary>
public record ProfileResponse
{
    [JsonPropertyName("profileId")]
    public Guid ProfileId { get; init; }

    [JsonPropertyName("length_m")]
    public decimal LengthM { get; init; }

    public List<ProfilePoint> Points { get; init; } = null!;

    public List<ProfilePoint> MainPoints { get; init; } = null!;
}

public class ProfileResponseExample : IExamplesProvider<ProfileResponse>
{
    public ProfileResponse GetExamples() =>
        new()
        {
            ProfileId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            LengthM   = 1234.56m,

            // Полный сглаженный профиль (400 точек в реальном ответе)
            Points = new List<ProfilePoint>
            {
                new(   0,  12.3,  true),   // попали ровно в изолинию
                new(  50,  13.1,  false),
                new( 100,  14.0,  true),   // снова изолиния
                new( 150,  14.8,  false)
            },

            // Подмножество точек, пересекающих изолинии
            MainPoints = new List<ProfilePoint>
            {
                new(0,   12.3, true),
                new(100, 14.0, true)
            }
        };
}