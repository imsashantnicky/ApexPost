# MVC action filters, auditing, and request security

## Registered filters

ApexPost registers these MVC filters through dependency injection:

- **AuditActionFilter** runs after state-changing MVC actions and records the controller/action, HTTP method, status, outcome, elapsed time, trace identifier, and a pseudonymous actor identifier. It deliberately does not serialize action arguments, form fields, request bodies, or response bodies.
- **PerformanceActionFilter** logs MVC action duration and outcome metadata. It does not log payloads.
- **ValidationActionFilter** is applied to the money-order controller. Invalid model state returns a 400 response with a `ValidationProblemDetails` body. Other MVC forms keep their existing view-oriented validation behavior.

## Why a filter instead of middleware?

Middleware runs around the HTTP pipeline and is suitable for cross-cutting HTTP concerns such as correlation IDs and response security headers. MVC action filters run in the MVC action pipeline, where route/action metadata and model state are available. Use filters for action auditing, validation, and action timing; use middleware for request-wide behavior.

## Audit data safety

Audit metadata is intentionally allow-listed. Never add passwords, access tokens, payment data, identity document numbers, addresses, phone numbers, or email addresses. The actor claim is hashed before it is written because this application's current account model uses a user name as its name identifier. The hash is pseudonymous, not anonymous, and should still be access-controlled.

The existing JSON audit store is suitable for the coursework/demo setup, not tamper-proof compliance logging. File-backed JSON is not an append-only security boundary: an operator with filesystem write access can alter it, and JSON persistence does not provide a database transaction across business data and audit data. A production deployment should send audit events to a separately permissioned append-only or centralized audit sink, alert on write failures, restrict log access, and test retention/backup procedures.

## Request security

`SecurityHeadersMiddleware` adds content-type, framing, referrer, permissions, and Content Security Policy headers. Authentication and antiforgery cookies are configured as Secure-only, HttpOnly, and SameSite-protected. Secure-only cookies require HTTPS, including during local browser testing; use the configured HTTPS launch profile.

The CSP is intentionally restrictive. If the UI adds inline scripts/styles or third-party assets, prefer nonces/hashes and an explicit allow-list instead of broadly enabling `unsafe-inline`.

## Verification checklist

- [ ] Verify each mutating action writes an audit record, including unsuccessful results.
- [ ] Verify audit files contain no request bodies, email addresses, passwords, document IDs, or payment data.
- [ ] Verify an audit-write failure is logged and surfaced to operations monitoring.
- [ ] Verify headers on both MVC and static-file responses.
- [ ] Verify the CSP does not break any Razor view assets.
- [ ] Run all tests and Release build with .NET 10 on the target environment.
