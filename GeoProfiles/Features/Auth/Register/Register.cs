using System.Net.Mime;
using FluentValidation;
using GeoProfiles.Features.Users;
using GeoProfiles.Infrastructure;
using GeoProfiles.Infrastructure.Examples;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Auth.Register;

public class Register(
    GeoProfilesContext db,
    ILogger<Register> logger
) : ControllerBase
{
    /// <summary>
    /// Регистрация нового пользователя.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("api/v1/register")]
    [Produces(MediaTypeNames.Application.Json)]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [SwaggerRequestExample(typeof(UserDataRequest), typeof(UserDataRequestExample))]
    [SwaggerResponseExample(StatusCodes.Status201Created, typeof(UserDtoExample))]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(BadRequestExample))]
    public async Task<IActionResult> Action
    (
        [FromBody] UserDataRequest request,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("Registering user...");

        // ReSharper disable once MethodHasAsyncOverloadWithCancellation
        new Validator().ValidateAndThrow(request);

        var user = await db.Users
            .Where(u => u.Email == request.Email)
            .SingleOrDefaultAsync(cancellationToken);

        if (user is not null)
        {
            logger.LogInformation("User already exists.");
            return BadRequest(new Errors.UserAlreadyExists("User already exists"));
        }

        var userEntity = new Model.Users
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = request.PasswordHash
        };

        db.Add(userEntity);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User registered.");

        return StatusCode(StatusCodes.Status201Created, userEntity.ToDto());
    }

    private class Validator : AbstractValidator<UserDataRequest>
    {
        public Validator()
        {
            RuleFor(r => r.Email).NotEmpty().EmailAddress();
            RuleFor(r => r.PasswordHash).NotEmpty();
        }
    }
}