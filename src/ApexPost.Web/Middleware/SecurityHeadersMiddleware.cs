namespace ApexPost.Web.Middleware;

/// <summary>
/// Adds baseline browser security headers to application responses.
/// The Content-Security-Policy is deliberately conservative for the app's same-origin UI.
/// If the UI uses external scripts, styles, or images, explicitly allowlist those origins.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        AddHeaders(context.Response.Headers);
        context.Response.OnStarting(() =>
        {
            AddHeaders(context.Response.Headers);
            return Task.CompletedTask;
        });

        await next(context);
    }

    private static void AddHeaders(IHeaderDictionary headers)
    {
        headers.TryAdd("X-Content-Type-Options", "nosniff");
        headers.TryAdd("X-Frame-Options", "DENY");
        headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        headers.TryAdd("Content-Security-Policy",
            "default-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'");
    }
}
