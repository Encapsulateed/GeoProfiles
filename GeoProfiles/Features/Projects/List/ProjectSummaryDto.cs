namespace GeoProfiles.Features.Projects.List;

public record ProjectSummaryDto
{
    public Guid             Id        { get; init; }
    public string           Name      { get; init; } = null!;
    public string           BboxWkt   { get; init; } = null!;
    public DateTimeOffset   CreatedAt { get; init; }
}
