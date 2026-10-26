using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

/// <summary>
/// Provides consistent model-validation responses for JSON/API requests.
/// MVC form actions retain control of redisplaying their view and validation messages.
/// </summary>
public sealed partial class ValidationActionFilter(ILogger<ValidationActionFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (!context.ModelState.IsValid && WantsJson(context.HttpContext.Request))
        {
            LogInvalidModel(logger, context.HttpContext.TraceIdentifier);

            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
                Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
            });
            return;
        }

        await next();
    }

    private static bool WantsJson(HttpRequest request) =>
        request.Headers.Accept.Any(value =>
            value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        || request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;

    [LoggerMessage(LogLevel.Information, "Request model validation failed. CorrelationId: {CorrelationId}")]
    private static partial void LogInvalidModel(ILogger logger, string correlationId);
}
