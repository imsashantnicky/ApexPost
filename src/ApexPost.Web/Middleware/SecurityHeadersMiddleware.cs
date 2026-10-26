namespace ApexPost.Web.Middleware;

/// <summary>Adds browser security headers to dynamic and static responses.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            headers.TryAdd("Content-Security-Policy", "default-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'");
            return Task.CompletedTask;
        });

        await next(context);
    }
}
