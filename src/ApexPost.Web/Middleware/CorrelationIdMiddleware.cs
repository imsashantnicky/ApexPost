using System.Diagnostics;

namespace ApexPost.Web.Middleware;

public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        this.next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var incomingId = context.Request.Headers.TryGetValue(HeaderName, out var incoming) ? incoming.ToString() : null;
        var correlationId = !string.IsNullOrEmpty(incomingId)
            && incomingId.Length <= 64
            && incomingId.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
                ? incomingId
                : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

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
}
