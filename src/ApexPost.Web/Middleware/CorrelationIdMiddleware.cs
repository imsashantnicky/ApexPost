
using System.Diagnostics;

namespace ApexPost.Web.Middleware;

/// <summary>
/// Establishes a safe correlation ID for request diagnostics.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-ID";
    private const int MaximumCorrelationIdLength = 64;

    private readonly RequestDelegate next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        this.next = next
            ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId =
            context.Request.Headers.TryGetValue(
                HeaderName, out var incoming)
            && IsSafeCorrelationId(incoming.ToString())
                ? incoming.ToString()
                : Activity.Current?.TraceId.ToString()
                    ?? Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;

        context.Response.Headers[HeaderName] = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using var activity = new Activity("ApexPostRequest");

        activity.SetParentId(correlationId);
        activity.Start();

        await next(context);
    }

    private static bool IsSafeCorrelationId(string value) =>
        value.Length is > 0 and <= MaximumCorrelationIdLength
        && value.All(static character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_' or '.');
}
