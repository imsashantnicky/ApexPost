# ApexPost architecture conventions

## Status

This document sets conventions for the existing solution. The repository is currently a starter scaffold: the business modules and most application services described here have not been implemented. These conventions do not imply that those features exist.

## Solution and responsibilities

The solution has four production projects and four test projects. All production projects target `net10.0`; nullable reference types and implicit usings are enabled.

| Project | Responsibility | May depend on |
| --- | --- | --- |
| `ApexPost.Domain` | Business entities, value objects, enums, domain policies, and invariant enforcement. Keep it independent of frameworks and storage. | .NET base libraries only |
| `ApexPost.Application` | Use cases, orchestration, input validation, application DTOs, and ports/interfaces required by use cases. | Domain |
| `ApexPost.Infrastructure` | Adapters for JSON persistence, serialization, repositories, caching, initialization, and external or simulated integrations. | Application, Domain, .NET and necessary infrastructure packages |
| `ApexPost.Web` | ASP.NET Core composition root, MVC controllers, Razor views, web view models, authentication/authorization configuration, middleware, and browser assets. | Application, Infrastructure, ASP.NET Core |

The dependency direction is inward: Web calls Application use cases; Infrastructure implements Application ports; Application uses Domain. Domain must not reference Application, Infrastructure, or Web. Infrastructure and Web must not become dependencies of Application or Domain. The current project references comply with these rules.

## Module boundaries

Organize business code by capability within the owning layer rather than creating one large cross-cutting service. Expected capabilities are:

- Branches and counters
- Staff and access roles
- Service catalogue and pricing
- Inventory
- Shipment booking and tracking
- Money orders
- Identity verification records
- Counter transactions and payments
- Receipts
- Reports and audit records

These are planned boundaries, not implemented modules. Keep each use case responsible for one business operation and its transaction boundary. A module may coordinate with another through explicit application interfaces or domain identifiers; avoid direct dependencies between unrelated persistence implementations. Do not imply integration with live postal, payment, or identity providers: external adapters remain simulated unless deliberately configured.

## Naming and placement

- Use the `ApexPost` root namespace, with namespaces matching project and folder names.
- Use `PascalCase` for types and public members, `camelCase` for parameters and locals, and descriptive names that state the business operation.
- Name use cases by intent, such as `BookShipment` or `RecordPayment`; use `I`-prefixed interfaces for ports and abstractions.
- Name request/response types for their boundary and operation (for example, `BookShipmentRequest` and `ShipmentReceiptDto`). Keep web-only binding types in Web and do not expose MVC view models as application contracts.
- Group related files in capability folders, then by layer-specific role where useful (`Entities`, `ValueObjects`, `UseCases`, `Repositories`, `Controllers`, `ViewModels`). Avoid generic placeholder names such as `Class1` and `UnitTest1` as implementation grows.
- Keep test namespaces and folder structures aligned with the production project and capability. Name tests for the behavior and expected outcome.

## Validation

Validate untrusted input at the Web boundary for request shape, format, and required fields, and repeat essential checks in Application so non-web callers cannot bypass them. Enforce business invariants in Domain constructors or behavior methods. Application coordinates validation and returns clear validation outcomes; it must not rely on client-side checks. Avoid duplicating business rules in controllers or Razor views. Do not accept client-supplied prices or role claims as authoritative without server-side verification.

## Error handling

- Use validation outcomes for expected invalid input and business-rule rejection; do not use exceptions for routine control flow.
- Use domain-specific exceptions only for violated invariants or impossible domain states, and translate them at the application/web boundary into stable outcomes.
- Let unexpected infrastructure failures retain their diagnostic exception for server-side logging. Return a safe generic error to the browser and never expose stack traces, secrets, credentials, tokens, or sensitive identity data.
- Log operational context with structured fields and correlation/request identifiers. Exclude passwords, tokens, identity documents, and unnecessary personal data.
- Persistence failures, including corrupt JSON, must be surfaced and must not silently reset or overwrite stored data.

## Dependency injection

Use constructor injection for application services and adapters. Register application use cases and their ports in an application registration method; register concrete infrastructure implementations in an infrastructure registration method. Web is the composition root and calls those registration methods during startup. Domain types should not require service registration. Prefer explicit, appropriately scoped lifetimes: stateless services may be singleton only when their dependencies are safe for singleton use; request-oriented application services are generally scoped; file-backed state must account for concurrent access. Do not resolve dependencies from a service locator in business code.

## Project reference audit

Current production references:

- Domain: none
- Application: Domain
- Infrastructure: Domain and Application
- Web: Application and Infrastructure

No architectural project-reference violations were found, so no project references were changed. Test projects currently have no project references to production code; adding those references is appropriate when meaningful tests are introduced, but is outside this conventions-only step.
