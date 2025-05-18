using Serilog.Context;

namespace GeoProfiles.Infrastructure.Middlewares;

public class RequestIdMiddleware(RequestDelegate next)
{
    private const string RequestIdHeader = "X-Request-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.Request.Headers.ContainsKey(RequestIdHeader)
            ? context.Request.Headers[RequestIdHeader].ToString()
            : Guid.NewGuid().ToString();

        context.Items[RequestIdHeader] = requestId;

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(RequestIdHeader))
            {
                context.Response.Headers.Append(RequestIdHeader, requestId);
            }

            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("RequestId", requestId))
        {
            await next(context);
        }
    }
}