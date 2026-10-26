using Microsoft.AspNetCore.Http;

namespace ApexPost.Web.Middleware;

public sealed class MaintenanceModeMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!configuration.GetValue<bool>("ApexPost:Maintenance:Enabled") || IsHealthEndpoint(context.Request.Path))
        {
            await next(context);
            return;
        }

        var retryAfter = Math.Clamp(configuration.GetValue<int?>("ApexPost:Maintenance:RetryAfterSeconds") ?? 300, 1, 86_400);
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = retryAfter.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Service temporarily unavailable.")
            .ExecuteAsync(context);
    }

    private static bool IsHealthEndpoint(PathString path)
    {
        var normalized = path.Value?.TrimEnd('/');
        return string.Equals(normalized, "/health", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "/health/ready", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "/health/live", StringComparison.OrdinalIgnoreCase);
    }
}
