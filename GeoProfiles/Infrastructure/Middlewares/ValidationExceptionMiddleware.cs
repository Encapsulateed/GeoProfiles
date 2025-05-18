using System.Net;
using System.Text.Json;
using FluentValidation;

namespace GeoProfiles.Infrastructure.Middlewares;

public class ValidationExceptionMiddleware(RequestDelegate next, ILogger<ValidationExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            context.Response.StatusCode = (int) HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";

            var errors = ex.Errors
                .Select(e => new {field = e.PropertyName, error = e.ErrorMessage})
                .ToArray();

            var payload = new
            {
                message = "Validation error",
                details = errors
            };

            logger.LogInformation("Validation error was processed");

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}