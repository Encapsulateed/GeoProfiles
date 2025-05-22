using System.Net.Mime;
using FluentValidation;
using GeoProfiles.Features.Auth.Register;
using GeoProfiles.Features.JWT;
using GeoProfiles.Infrastructure;
using GeoProfiles.Infrastructure.Examples;
using GeoProfiles.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.Auth.Login;


public class Login(
    GeoProfilesContext db,
    ITokenService tokenService,
    ILogger<Login> logger
) : ControllerBase
{
    private const int ExpiresIn = 3600;

    /// <summary>
    /// Авторизация пользователя.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    [HttpPost("api/v1/auth/login")]
    [Produces(MediaTypeNames.Application.Json)]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TokenDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorResponse))]
    [SwaggerRequestExample(typeof(UserDataRequest), typeof(UserDataRequestExample))]
    [SwaggerResponseExample(StatusCodes.Status200OK, typeof(TokenDtoExample))]
    [SwaggerResponseExample(StatusCodes.Status400BadRequest, typeof(BadRequestExample))]
    public async Task<IActionResult> Post([FromBody] UserDataRequest request)
    {
        logger.LogInformation("Login attempt for {Email}", request.Email);

        new Validator().ValidateAndThrow(request);

        var user = await db.Users
            .SingleOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || user.PasswordHash != request.PasswordHash)
        {
            logger.LogWarning("Invalid credentials for {Email}", request.Email);

            return Unauthorized(new ErrorResponse(
                ErrorCode: "invalid_credentials",
                ErrorMessage: "Invalid email or password"
            ));
        }

        var (accessToken, refreshToken) = tokenService.GenerateTokens(user.Id, user.Username, []);

        var dto = new TokenDto
        {
            Token = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = ExpiresIn
        };

        return Ok(dto);
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