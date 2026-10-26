using System.Diagnostics;
using System.Security.Claims;
using ApexPost.Infrastructure.Persistence.Json;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ApexPost.Web.Filters;

/// <summary>
/// Records request/action outcome metadata without recording request bodies, query values,
/// route values, headers, or model data that could contain secrets or personal information.
/// </summary>
public sealed class AuditActionFilter(
    JsonAuditWriter auditWriter,
    ILogger<AuditActionFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        ActionExecutedContext? executed = null;
        var actionFailed = false;

        try
        {
            executed = await next();
        }
        catch
        {
            actionFailed = true;
            throw;
        }
        finally
        {
            stopwatch.Stop();

            var httpContext = context.HttpContext;
            var controller = context.RouteData.Values["controller"]?.ToString() ?? "Unknown";
            var action = context.RouteData.Values["action"]?.ToString() ?? "Unknown";
            var statusCode = actionFailed
                ? StatusCodes.Status500InternalServerError
                : GetStatusCode(executed, httpContext.Response.StatusCode);
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HttpMethod"] = httpContext.Request.Method,
                ["StatusCode"] = statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["ElapsedMs"] = stopwatch.ElapsedMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["CorrelationId"] = httpContext.TraceIdentifier
            };

            try
            {
                var actorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
                await auditWriter.WriteAsync(
                    "Mvc.ActionCompleted",
                    "MvcAction",
                    $"{controller}.{action}",
                    actorId,
                    metadata,
                    httpContext.RequestAborted);
            }
            catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
            {
                // Client cancellation must not be reported as an application fault.
            }
            catch (Exception exception)
            {
                // Do not let a secondary audit-store failure hide the original action result.
                logger.LogError(exception,
                    "Unable to persist MVC action audit record. CorrelationId: {CorrelationId}",
                    httpContext.TraceIdentifier);
            }
        }
    }

    private static int GetStatusCode(ActionExecutedContext? executed, int responseStatusCode)
    {
        if (executed?.Exception is not null && !executed.ExceptionHandled)
        {
            return StatusCodes.Status500InternalServerError;
        }

        return executed?.Result is IStatusCodeActionResult { StatusCode: not null } result
            ? result.StatusCode.Value
            : responseStatusCode;
    }
}
