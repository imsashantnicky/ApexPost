using System.Globalization;
using System.Security.Claims;
using ApexPost.Application.Auditing;
using ApexPost.Application.Security;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ApexPost.Web.Filters;

/// <summary>
/// Opts a read-only action into MVC auditing. State-changing requests (anything other than GET, HEAD or
/// OPTIONS) are always audited; plain page views are not.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class AuditedAttribute : Attribute, IFilterMetadata;

/// <summary>
/// Audits state-changing business actions: staff, role, branch, controller, action, the <c>id</c> route value,
/// result status, duration and correlation ID. Request bodies, query strings, headers and model values are
/// never recorded, so passwords, document numbers, API keys and card data cannot reach the audit log.
/// </summary>
public sealed class AuditActionFilter(AuditTrail auditTrail, TimeProvider timeProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!ShouldAudit(context))
        {
            await next();
            return;
        }

        var started = timeProvider.GetTimestamp();
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
            var httpContext = context.HttpContext;
            var controller = context.RouteData.Values["controller"]?.ToString() ?? "Unknown";
            var action = context.RouteData.Values["action"]?.ToString() ?? "Unknown";
            var statusCode = actionFailed
                ? StatusCodes.Status500InternalServerError
                : GetStatusCode(executed, httpContext.Response.StatusCode);
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["HttpMethod"] = httpContext.Request.Method,
                ["Controller"] = controller,
                ["Action"] = action,
                ["StatusCode"] = statusCode.ToString(CultureInfo.InvariantCulture),
                ["ElapsedMs"] = ((long)timeProvider.GetElapsedTime(started).TotalMilliseconds).ToString(CultureInfo.InvariantCulture),
                [AuditMetadataPolicy.CorrelationIdKey] = httpContext.TraceIdentifier
            };
            if (RouteId(context) is { } routeId) metadata["RouteId"] = routeId;

            var actorId = httpContext.User.FindFirstValue(ActorClaimTypes.StaffId);
            var branchId = Guid.TryParse(httpContext.User.FindFirstValue(ActorClaimTypes.AssignedBranchId), out var parsedBranch) ? parsedBranch : (Guid?)null;
            var role = httpContext.User.FindFirstValue(ClaimTypes.Role);
            ActorContext? actor = Guid.TryParse(actorId, out var parsedActor) && !string.IsNullOrWhiteSpace(role)
                ? new ActorContext(parsedActor, role, branchId, httpContext.TraceIdentifier)
                : null;
            await auditTrail.RecordAsync(actor, branchId, "Mvc.ActionCompleted", "MvcAction", $"{controller}.{action}",
                statusCode < 400 ? AuditTrail.Succeeded : AuditTrail.Failed, metadata, httpContext.RequestAborted);
        }
    }

    private static bool ShouldAudit(ActionExecutingContext context) =>
        !(HttpMethods.IsGet(context.HttpContext.Request.Method) || HttpMethods.IsHead(context.HttpContext.Request.Method) || HttpMethods.IsOptions(context.HttpContext.Request.Method))
        || context.Filters.OfType<AuditedAttribute>().Any();

    // Only identifier-shaped ids are kept; anything else (free text in a route) is dropped, not recorded.
    private static string? RouteId(ActionExecutingContext context) =>
        context.RouteData.Values.TryGetValue("id", out var value) && value?.ToString() is { } id && AuditMetadataPolicy.IsAllowed("RouteId", id) ? id : null;

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
