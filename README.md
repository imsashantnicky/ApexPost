# ApexPost

ApexPost is a secure ASP.NET Core MVC application for money-order operations and branch management.

## Secure first-user setup

The application does not ship with a default production account or embedded password. On first run, when no accounts exist, navigate to `/Account/Bootstrap` and create the first administrator account with a strong password (12+ characters, unique to this environment).

Recommended setup:

- Use a dedicated administrator name that is not shared with a standard staff user.
- Choose a password with at least 12 characters and a mix of character types.
- Use a unique secret for each environment; never reuse a development password in production.
- After the first login, provision additional accounts from `/Account/Provision` with the minimum role required for the user's duties.

## Authentication and authorization

- Authentication uses ASP.NET Core cookie authentication with secure cookies, anti-forgery validation, and rate limiting on login attempts.
- Passwords are never stored in plaintext; only PasswordHasher output is persisted.
- The `Management` policy allows administrators and branch managers to access operational management screens.
- The `AdministratorOnly` policy restricts account provisioning to administrators.
- Unauthorized users are redirected to `/Account/AccessDenied`.

## Operational notes

- Login attempts are rate-limited to reduce brute-force abuse.
- Cookie settings enforce HTTPS and HTTP-only behavior.
- Forms use ASP.NET Core antiforgery tokens to prevent CSRF attacks.
