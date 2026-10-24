# ADR-005: ASP.NET Core cookie authentication with local accounts

- Status: Accepted
- Related files: `src/ApexPost.Web/Program.cs`, `src/ApexPost.Web/Controllers/AccountController.cs`, `src/ApexPost.Web/Services/LocalAccountService.cs`

## Context

ApexPost is an MVC app with management and admin-only workflows. The design needs local sign-in, sign-out, role-based authorization, secure cookie handling, and protection from repeated login abuse without custom token logic.

## Decision

The application uses ASP.NET Core cookie authentication with claims-based roles:

- `Management` policy requires `Administrator` or `BranchManager`
- `AdministratorOnly` policy requires `Administrator`
- password hashes are stored using `PasswordHasher<AccountRecord>`
- login requests are protected by a fixed-window rate limiter
- antiforgery validation is enabled on state-changing form actions
- cookies are configured as secure, HttpOnly, and HTTPS-only
- the first account is created through `/Account/Bootstrap`
- additional local users are created through `/Account/Provision` by an administrator

## Alternatives considered

1. Custom session or token logic
   - rejected because the ASP.NET Core cookie model is native, secure, and better supported
2. Hardcoded default credentials
   - rejected because the repository must not ship production-ready defaults
3. No account provisioning workflow
   - rejected because the app requires a controlled first-user and role assignment path

## Consequences

- Local sign-in is straightforward and integrates cleanly with MVC controllers.
- The app aligns with ASP.NET Core conventions and avoids custom security surfaces.
- Production identity provider integration remains a future extension.

## Security and operational implications

This design is secure for local and internal deployments when hosted behind HTTPS. It still requires careful environment configuration, role assignment discipline, and secret hygiene before production use.

## Related decisions

- ADR-001: Layered architecture
- ADR-006: Audit redaction and operational logging
