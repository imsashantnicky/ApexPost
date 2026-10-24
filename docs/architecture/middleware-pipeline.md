# Middleware and request pipeline

## Actual startup pipeline

The application pipeline in `src/ApexPost.Web/Program.cs` is intentionally narrow and explicit:

```mermaid
flowchart LR
    A[Browser request] --> B[UseExceptionHandler]
    B --> C[UseHsts]
    C --> D[UseHttpsRedirection]
    D --> E[CorrelationIdMiddleware]
    E --> F[UseRouting]
    F --> G[UseCookiePolicy]
    G --> H[UseRateLimiter]
    H --> I[UseAuthentication]
    I --> J[UseAuthorization]
    J --> K[Static assets / MVC endpoints]
    K --> L[Health checks]
```

## Middleware details

1. `UseExceptionHandler` handles unhandled exceptions and routes to the MVC error page.
2. `UseHsts` is enabled outside Development mode.
3. `UseHttpsRedirection` redirects HTTP requests to HTTPS when the host is configured for HTTPS.
4. `CorrelationIdMiddleware` adds a request correlation ID and includes it in the response header.
5. `UseRouting` matches the route table to the request.
6. `UseCookiePolicy` keeps cookie behavior aligned with configured browser security settings.
7. `UseRateLimiter` applies the fixed-window login limiter.
8. `UseAuthentication` and `UseAuthorization` enforce identity and role policies.
9. Static assets and controller endpoints are then processed.
10. Health endpoints are mapped directly without exposing additional operational secrets.

## Security notes

- Rate limiting is enabled specifically for the login endpoint.
- Authorization policies are enforced before controller actions run.
- Cookies are configured with secure, HttpOnly, and SameSite-aware settings.
- Antiforgery tokens are validated globally for MVC actions through `AutoValidateAntiforgeryToken`.

## Current limitations

This is a simple server-side request pipeline. It does not currently include distributed tracing, advanced request logging, or a dedicated security filtering layer beyond the project’s explicit auth, health, and correlation components.
