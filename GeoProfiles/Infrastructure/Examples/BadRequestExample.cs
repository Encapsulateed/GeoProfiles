using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Infrastructure.Examples;

public class BadRequestExample : IExamplesProvider<ErrorResponse>
{
    public ErrorResponse GetExamples()
    {
        return new ErrorResponse(
            ErrorCode: "validation_error",
            ErrorMessage: "One or more validation errors occurred.",
            ErrorDetails: new[]
            {
                new {Field = "param", Error = "\"param\" must be greater than 10 characters."}
            }
        );
    }
}