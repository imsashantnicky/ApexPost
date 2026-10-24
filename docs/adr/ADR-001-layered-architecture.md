# ADR-001: Layered architecture

- Status: Accepted
- Related files: `src/ApexPost.Domain`, `src/ApexPost.Application`, `src/ApexPost.Infrastructure`, `src/ApexPost.Web`

## Context

ApexPost is a business-focused ASP.NET Core MVC solution for branch operations, inventory, shipment tracking, money orders, reporting, and secure local authentication. The project needs a reusable boundary between business rules, application orchestration, persistence, and HTTP concerns.

## Decision

The solution is organized into four production layers:

- `ApexPost.Domain` owns entities, value objects, and business invariants.
- `ApexPost.Application` owns use cases, DTOs, validation outcomes, and repository interfaces.
- `ApexPost.Infrastructure` implements repository adapters, JSON persistence, seed initialization, and audit behavior.
- `ApexPost.Web` contains the ASP.NET Core MVC composition root, controllers, views, and browser assets.

The dependency direction is intentional: `Web -> Application -> Domain`, while `Infrastructure` implements interfaces declared by `Application` and `Domain` without referencing `Web`.

## Alternatives considered

1. A single MVC project with business logic in controllers
   - simpler to start but harder to test and maintain
2. Domain logic in the infrastructure layer
   - rejected because persistence cannot define business rules
3. Direct repository calls from views or controllers
   - rejected because it bypasses validation and use-case boundaries

## Consequences

- Domain invariants stay centralized and testable.
- Application behavior can be validated without ASP.NET Core or JSON file I/O.
- Future persistence changes can be isolated behind repository abstractions.
- The Web layer remains focused on request handling, auth, and presentation.

## Security and operational implications

The layer boundary makes authentication and authorization easier to enforce at the HTTP boundary while preserving business behavior in the domain and application layers. It also reduces accidental leakage of business logic into UI code.

## Related decisions

- ADR-003: JSON persistence model
- ADR-005: Authentication and role-based authorization
- ADR-006: Audit redaction and operational logging
- ADR-007: Mixed transaction and payment model
