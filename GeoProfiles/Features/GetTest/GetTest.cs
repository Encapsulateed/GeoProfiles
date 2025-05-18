using System.Net.Mime;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace GeoProfiles.Features.GetTest;

public class GetTest(ILogger<GetTest> logger) : ControllerBase
{
    [HttpGet("api/v1/get-test")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(Ok), StatusCodes.Status200OK)]
    public Task<IActionResult> Get([FromQuery] string param)
    {
        new Validator().ValidateAndThrow(param);

        return Task.FromResult<IActionResult>(Ok());
    }

    private class Validator : AbstractValidator<string>
    {
        public Validator()
        {
            RuleFor(param => param.Length).GreaterThan(10);
        }
    }
}