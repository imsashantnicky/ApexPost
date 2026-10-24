# ApexPost design and delivery notes

## Purpose

ApexPost is a branch-operations and money-order application built as an ASP.NET Core MVC solution. It combines a domain-driven business model with JSON-backed persistence for local development and controlled internal deployments. The design keeps the application simple to restore and operate while preserving explicit invariants, fail-safe validation, and security controls.

## Architectural layers

### Domain

The `ApexPost.Domain` project contains the business entities and value objects that own rules and invariants. Examples include branch records, staff members, shipment lifecycles, money orders, inventory, payment records, and transaction totals.

This layer is intentionally framework-independent and should not depend on ASP.NET Core, Infrastructure, or Application code.

### Application

The `ApexPost.Application` project contains use cases, DTOs, validation outcomes, and interfaces for repositories and external adapters. It coordinates business workflows and returns explicit result objects rather than leaking low-level exceptions to the UI.

The application depends on Domain but not on Web or Infrastructure.

### Infrastructure

The `ApexPost.Infrastructure` project adapts the application to the chosen persistence model. It writes JSON files, seeds data, initializes the operational JSON directory, and ensures malformed content fails loudly instead of silently overwriting existing data.

The persistence model is intentionally file-based for this stage, so the data directory is part of the runtime configuration and must be treated as operational state.

### Web

The `ApexPost.Web` project is the ASP.NET Core composition root. It wires authentication, authorization, middleware, controllers, Razor views, browser assets, and health endpoints. The Web layer does not own business rules; it coordinates HTTP requests and calls application use cases.

## Data flow

1. Users authenticate with ASP.NET Core cookie authentication.
2. Authorization policies restrict management, operational, and admin actions.
3. Controllers call application use cases such as inventory, branch, or shipment operations.
4. Use cases validate business rules and delegate persistence through repository interfaces.
5. Infrastructure implementations read and write JSON collections to the configured App_Data or deployment-specific path.
6. Reporting and operational services aggregate repository output for authorized management views.
7. Browser-side IndexedDB is used only for non-sensitive draft and queue metadata; it never stores server-confirmed operations or credentials.

## Key decisions

### ASP.NET Core cookie authentication

Cookie authentication is the native and well-supported method for an MVC app that needs local account sign-in, claims, and secure session handling. It avoids custom token logic and supports built-in antiforgery and cookie configuration hooks.

### Role policies over ad hoc checks

The app defines named authorization policies such as `Management` and `AdministratorOnly` rather than scattering role checks in controllers. This keeps business access rules centralized and future-ready.

### Password hashes only

User accounts are created with ASP.NET Core PasswordHasher and only the hash is persisted. Plaintext passwords are never stored.

### JSON file persistence for current phase

The repository uses file-backed JSON for simplicity, local deployment, and repeatable setup. This is a pragmatic choice for a focused MVP and internal rollout, but it is not the final persistence target for many production workloads.

### Safe browser-local storage

IndexedDB is restricted to non-sensitive local drafts and queue metadata. Sensitive values like passwords, tokens, session material, or identity details are explicitly rejected.

## Security controls in scope

- Secure cookie settings: HTTPS-only, HttpOnly, SameSite, expiration, and sliding renewal
- Anti-forgery validation on state-changing forms
- Rate limiting for repeated login attempts
- Strong first-user bootstrap requirements
- Access-denied handling and role restrictions
- Safe audit metadata filtering and redaction of sensitive fields
- Health checks that avoid disclosing secrets or runtime configuration
- Correlation IDs for request tracing without leaking sensitive details

## Known limitations

- The project currently uses local JSON persistence rather than a transactional database.
- There is no multi-instance distributed coordination or cross-node locking yet.
- Payment and identity integration remain intentionally simulated and are not external provider integrations.
- Browser storage remains local to the user's device and can be removed or tampered with by the user.
- This is a focused engineering review, not a full production security audit.

## Future database migration plan

1. Introduce a relational or document database backed by explicit connection configuration and secret management.
2. Move branch, staff, inventory, shipment, money-order, and audit records behind persisted repository interfaces with migration scripts.
3. Add transactional boundaries and integrity checks around multi-step business operations.
4. Introduce operational backup automation and restore validation procedures.
5. Keep the current Web and Application layers stable while swapping Infrastructure adapters behind their interfaces.

## Delivery realism

The repository is suitable for local development, controlled internal deployment, and hands-on evaluation. It is not a complete production service with multi-instance cluster support, configured external authentication, or a managed database. The design is intentionally honest about those constraints and documents them as explicit future work.
