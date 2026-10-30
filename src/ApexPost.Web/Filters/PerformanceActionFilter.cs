using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

/// <summary>Logs slow MVC actions without logging request payloads or query-string values.</summary>
public sealed partial class PerformanceActionFilter(
    IConfiguration configuration,
    ILogger<PerformanceActionFilter> logger,
    TimeProvider timeProvider) : IAsyncActionFilter
{
    private readonly int thresholdMilliseconds = Math.Clamp(
        configuration.GetValue<int?>("ApexPost:Diagnostics:SlowActionThresholdMilliseconds") ?? 500,
        1,
        60_000);

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var started = timeProvider.GetTimestamp();
        try
        {
            await next();
        }
        finally
        {
            var elapsedMilliseconds = (long)timeProvider.GetElapsedTime(started).TotalMilliseconds;

            if (elapsedMilliseconds >= thresholdMilliseconds)
            {
                LogSlowAction(logger,
                    context.RouteData.Values["controller"]?.ToString() ?? "Unknown",
                    context.RouteData.Values["action"]?.ToString() ?? "Unknown",
                    elapsedMilliseconds,
                    context.HttpContext.TraceIdentifier);
            }
        }
    }

    [LoggerMessage(LogLevel.Warning, "Slow MVC action. Controller: {Controller}; Action: {Action}; ElapsedMs: {ElapsedMs}; CorrelationId: {CorrelationId}")]
    private static partial void LogSlowAction(ILogger logger, string controller, string action, long elapsedMs, string correlationId);
}
