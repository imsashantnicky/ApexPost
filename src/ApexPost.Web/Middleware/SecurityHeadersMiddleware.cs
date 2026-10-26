
namespace ApexPost.Web.Middleware;

/// <summary>
/// Adds baseline browser security headers to application responses.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(() =>
        {
            AddHeaders(context.Response.Headers);
            return Task.CompletedTask;
        });

        AddHeaders(context.Response.Headers);

        await next(context);
    }

    private static void AddHeaders(IHeaderDictionary headers)
    {
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=()";

        headers["Content-Security-Policy"] =
            "default-src 'self'; object-src 'none'; base-uri 'self'; " +
            "frame-ancestors 'none'; form-action 'self'";
    }
}
