# ADR-002: Domain model and business invariants

- Status: Accepted
- Related files: `src/ApexPost.Domain`, `tests/ApexPost.Domain.Tests`

## Context

ApexPost manages branch operations, staff management, catalogue and pricing, inventory, money orders, shipments, and retail transactions. These features require explicit invariants such as valid codes, role enforcement, payment-state transitions, and shipment lifecycle rules.

## Decision

The domain model is implemented as entities and value objects in `ApexPost.Domain`. Key aggregates include:

- `Branch`
- `StaffMember`
- `ServiceDefinition`
- `InventoryItem`
- `Shipment`
- `MoneyOrder`
- `RetailTransaction`
- `PaymentRecord`
- `IdentityVerificationRecord`

Business rules live in constructors and aggregate behavior instead of controllers and repositories. Domain exceptions are reserved for impossible or broken invariants; expected validation failures remain in application results.

## Alternatives considered

1. Put all state validation in the Web layer
   - rejected because UI code should not define business rules
2. Model everything as DTOs and store plain strings
   - rejected because the solution needs enforceable domain semantics
3. One giant aggregate for every capability
   - rejected because capability boundaries are distinct and easier to evolve separately

## Consequences

- Business rules can be tested without HTTP or JSON persistence.
- Application use cases remain focused on orchestration and validation.
- Repository implementations follow aggregate boundaries and versioning semantics.

## Security and operational implications

The domain model keeps server-side validation authoritative, especially for pricing, shipment lifecycle, and payment states. This lowers the chance that untrusted client input bypasses intended rules.

## Related decisions

- ADR-001: Layered architecture
- ADR-007: Mixed transaction and payment model
- ADR-006: Audit redaction and operational logging
