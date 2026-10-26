using System.Diagnostics;
using System.Security.Claims;
using ApexPost.Infrastructure.Persistence.Json;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

/// <summary>
/// Audits state-changing MVC requests without serializing action arguments or request bodies.
/// Never put credentials, document IDs, addresses, or payment data into audit metadata.
/// </summary>
public sealed class AuditActionFilter(
    JsonAuditWriter auditWriter,
    ILogger<AuditActionFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) ||
            HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            await next();
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var executed = await next();
        stopwatch.Stop();

        var route = context.RouteData.Values;
        var controller = route.GetValueOrDefault("controller")?.ToString() ?? "Unknown";
        var action = route.GetValueOrDefault("action")?.ToString() ?? "Unknown";
        var actorId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var statusCode = executed.HttpContext.Response.StatusCode;
        var outcome = executed.Exception is not null && !executed.ExceptionHandled
            ? "exception"
            : statusCode >= 400 ? "failed" : "completed";

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["httpMethod"] = method,
            ["routeController"] = controller,
            ["routeAction"] = action,
            ["statusCode"] = statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["outcome"] = outcome,
            ["durationMs"] = stopwatch.ElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["correlationId"] = context.HttpContext.TraceIdentifier
        };

        try
        {
            await auditWriter.WriteAsync(
                action: $"{controller}.{action}",
                entityType: "MvcAction",
                entityId: $"{controller}/{action}",
                actorId: actorId,
                metadata: metadata,
                cancellationToken: context.HttpContext.RequestAborted);
        }
        catch (Exception exception)
        {
            // Audit failures are visible to operations; do not log request bodies or exception messages
            // because they may contain untrusted or sensitive values.
            logger.LogError(
                "Audit write failed for {Controller}.{Action}; TraceIdentifier={TraceIdentifier}; ExceptionType={ExceptionType}",
                controller, action, context.HttpContext.TraceIdentifier, exception.GetType().Name);
            // Do not turn an already-executed mutation into a misleading HTTP failure.
            // Production deployments should alert on this event and use durable audit storage.
        }
    }
}
