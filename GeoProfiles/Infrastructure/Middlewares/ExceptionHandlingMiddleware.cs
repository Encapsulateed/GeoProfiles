using System.Net;
using System.Text.Json;
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