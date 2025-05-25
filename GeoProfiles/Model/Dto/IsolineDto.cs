using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Model.Dto;

public record IsolineDto
{
    public int Level { get; init; }

    public string GeomWkt { get; init; } = null!;
}

public sealed class IsolineDtoExample : IExamplesProvider<IsolineDto>
{
    public IsolineDto GetExamples()
    {
        return new IsolineDto
        {
            Level = 0,
            GeomWkt = "POLYGON((-0.001 -0.001, 0.001 -0.001, 0.001 0.001, -0.001 0.001, -0.001 -0.001))"
        };
    }
}