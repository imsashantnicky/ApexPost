# ADR-008: MVC action filters and audit metadata

- **Status:** Accepted for the coursework implementation
- **Date:** 2026-10-26

## Context

The project needs action-level auditing and validation in addition to HTTP-wide middleware. Logging entire action arguments or request bodies risks storing credentials, identity documents, contact details, or payment data.

## Decision

Register an `AuditActionFilter` and `PerformanceActionFilter` globally through dependency injection. Use `ValidationActionFilter` on money-order endpoints, where a structured 400 validation response is appropriate. Keep request correlation and response security headers in middleware. Audit only metadata and hash the current username-based actor identifier before persistence.

## Consequences

- MVC action name, status, timing, and trace identifier are available to audit/logging.
- Action arguments and request bodies are not serialized.
- Audit persistence remains JSON-backed and is not tamper-proof or transactionally coupled to business writes.
- A production system should use a separately permissioned durable audit sink and alert on write failures.
- The restrictive Content Security Policy must be checked against all Razor view assets during browser testing.
