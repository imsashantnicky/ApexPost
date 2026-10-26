using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

/// <summary>Logs slow MVC actions without logging request payloads or query-string values.</summary>
public sealed class PerformanceActionFilter(
    IConfiguration configuration,
    ILogger<PerformanceActionFilter> logger) : IAsyncActionFilter
{
    private readonly int thresholdMilliseconds = Math.Clamp(
        configuration.GetValue<int?>("ApexPost:Diagnostics:SlowActionThresholdMilliseconds") ?? 500,
        1,
        60_000);

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next();
        }
        finally
        {
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds >= thresholdMilliseconds)
            {
                logger.LogWarning(
                    "Slow MVC action. Controller: {Controller}; Action: {Action}; ElapsedMs: {ElapsedMs}; CorrelationId: {CorrelationId}",
                    context.RouteData.Values["controller"]?.ToString() ?? "Unknown",
                    context.RouteData.Values["action"]?.ToString() ?? "Unknown",
                    stopwatch.ElapsedMilliseconds,
                    context.HttpContext.TraceIdentifier);
            }
        }
    }
}
