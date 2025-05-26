using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Profiles.Create;

public record ProfileRequest(double[] Start, double[] End);

public class ProfileRequestExample : IExamplesProvider<ProfileRequest>
{
    public ProfileRequest GetExamples() =>
        new(
            Start: [30.123, 59.987],
            End: [30.456, 60.012]
        );
}