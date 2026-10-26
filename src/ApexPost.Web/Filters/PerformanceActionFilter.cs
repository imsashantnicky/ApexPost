using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

/// <summary>Measures MVC action execution without recording request or response bodies.</summary>
public sealed class PerformanceActionFilter(ILogger<PerformanceActionFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        var executed = await next();
        stopwatch.Stop();

        logger.LogInformation(
            "MVC action completed. Controller={Controller}; Action={Action}; Method={Method}; StatusCode={StatusCode}; DurationMs={DurationMs}; TraceIdentifier={TraceIdentifier}",
            context.RouteData.Values.GetValueOrDefault("controller"),
            context.RouteData.Values.GetValueOrDefault("action"),
            context.HttpContext.Request.Method,
            executed.HttpContext.Response.StatusCode,
            stopwatch.ElapsedMilliseconds,
            context.HttpContext.TraceIdentifier);
    }
}
