using System.Text.Json.Serialization;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Profiles.List;

public record ProfileListItem
{
    [JsonPropertyName("id")] public Guid Id { get; init; }

    [JsonPropertyName("start")] public double[] Start { get; init; } = null!;

    [JsonPropertyName("end")] public double[] End { get; init; } = null!;

    [JsonPropertyName("length_m")] public decimal LengthM { get; init; }

    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; init; }
}

public record ProfileList
{
    public IList<ProfileListItem> Items { get; init; } = null!;
}

public class ProfileListExample : IExamplesProvider<ProfileList>
{
    public ProfileList GetExamples() => new ProfileList
    {
        Items = new List<ProfileListItem>
        {
            new ProfileListItem {
                Id         = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Start      = [30.123, 59.987],
                End        = [30.456, 60.012],
                LengthM    = 1234.56m,
                CreatedAt  = DateTime.Parse("2025-05-01T12:34:56Z")
            },
            new ProfileListItem {
                Id         = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Start      = [30.200, 59.990],
                End        = [30.500, 60.000],
                LengthM    = 1500.00m,
                CreatedAt  = DateTime.Parse("2025-05-02T08:15:00Z")
            }
        }
    };
}