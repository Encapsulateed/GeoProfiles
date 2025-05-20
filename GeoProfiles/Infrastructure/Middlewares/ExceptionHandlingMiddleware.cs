using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        catch (ValidationException ve)
        {
            var errors = ve.Errors
                .Select(e => new {field = e.PropertyName, error = e.ErrorMessage})
                .ToArray();

            var responseObj = new ErrorResponse(
                ErrorCode: "VALIDATION_ERROR",
                ErrorMessage: "Validation failed",
                ErrorDetails: errors
            );

            await WriteResponse(context, HttpStatusCode.BadRequest, responseObj);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method,
                context.Request.Path);

            var responseObj = new ErrorResponse(
                ErrorCode: "INTERNAL_ERROR",
                ErrorMessage: "An unexpected error occurred.",
                ErrorDetails: ex.Message
            );

            await WriteResponse(context, HttpStatusCode.InternalServerError, responseObj);
        }
    }

    private static async Task WriteResponse(HttpContext context, HttpStatusCode statusCode, ErrorResponse error)
    {
        context.Response.StatusCode = (int) statusCode;
        context.Response.ContentType = "application/json";

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(error, options);
        await context.Response.WriteAsync(json);
    }
}