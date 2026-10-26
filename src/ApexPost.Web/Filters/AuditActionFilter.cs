using System.Diagnostics;
using System.Security.Claims;
using ApexPost.Application.Security;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ApexPost.Web.Filters;

/// <summary>
/// Records request/action outcome metadata without recording request bodies, query values,
/// route values, headers, or model data that could contain secrets or personal information.
/// </summary>
public sealed class AuditActionFilter(
    IAccessAuditWriter auditWriter) : IAsyncActionFilter
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

            var actorId = httpContext.User.FindFirstValue(ActorClaimTypes.StaffId);
            var branchClaim = httpContext.User.FindFirstValue(ActorClaimTypes.AssignedBranchId);
            var branchId = Guid.TryParse(branchClaim, out var parsedBranch) ? parsedBranch : (Guid?)null;
            var role = httpContext.User.FindFirstValue(ClaimTypes.Role);
            ActorContext? actor = Guid.TryParse(actorId, out var parsedActor) && !string.IsNullOrWhiteSpace(role)
                ? new ActorContext(parsedActor, role, branchId, httpContext.TraceIdentifier)
                : null;
            await auditWriter.WriteOperationAsync(actor, branchId, "Mvc.ActionCompleted", "MvcAction",
                $"{controller}.{action}", statusCode < 400 ? "Succeeded" : "Failed", metadata, httpContext.RequestAborted);
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
