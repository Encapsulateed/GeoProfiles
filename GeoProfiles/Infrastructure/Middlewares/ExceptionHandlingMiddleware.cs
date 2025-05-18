using System.Net;
using System.Text.Json;
using FluentValidation;
using Serilog;

namespace GeoProfiles.Infrastructure.Middlewares;

public class ExceptionHandlingMiddleware(RequestDelegate next)
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

            Log.Information("Validation error was processed");

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }

        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception occurred while processing request {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = (int) HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var errorResponse = new
            {
                error = new
                {
                    message = "An unexpected error occurred.",
                    detail = ex.Message
                }
            };

            var json = JsonSerializer.Serialize(errorResponse);
            await context.Response.WriteAsync(json);
        }
    }
}