# ADR-006: Application-boundary auditing with safe failure semantics

- Status: Accepted
- Related files: `src/ApexPost.Application/Security/OperationalAccess.cs`, `src/ApexPost.Application/Auditing/AuditTrail.cs`, `src/ApexPost.Application/Auditing/AuditMetadataPolicy.cs`, `src/ApexPost.Infrastructure/Persistence/Json/JsonLinesAuditStore.cs`, `src/ApexPost.Web/Filters/AuditActionFilter.cs`

## Context

Operational systems need an append-only audit trail for sensitive state changes and rejected access while avoiding secrets, payment data, identity-document values, authentication material, and free-form request content. MVC-only auditing is insufficient because application use cases can also run outside HTTP. JSON persistence remains the selected storage architecture.

## Decision

Use cases and `OperationalAccess` record events through `AuditTrail`, which appends to the `IAuditStore` port; Infrastructure implements it with `JsonLinesAuditStore`. Records carry a `TimeProvider` UTC timestamp, actor/staff ID and role when resolved, validated branch, operation, target, outcome, correlation ID, and allowlisted safe metadata. Sensitive mutations append `Started` before business persistence and a terminal event afterward. Authorization denials are recorded at the Application access boundary. The MVC `AuditActionFilter` audits only state-changing requests (not GET, HEAD or OPTIONS) and actions marked `[Audited]`, recording staff, role, branch, controller, action, an identifier-shaped `id` route value, status code, elapsed time and correlation ID; it propagates audit failures. List reads authorize the actor once and filter by branch scope, so a list writes at most one (denial) entry.

`AuditMetadataPolicy` allow-lists every metadata key and gives each a format: operation names must match `^[A-Za-z]+(\.[A-Za-z]+)+$`, correlation IDs follow the `CorrelationIdMiddleware` rule, `Status` accepts domain status names or result codes that pass a sensitive-keyword scan, and numbers are too short to hold a card number. Format checks replace the earlier keyword scan for structured values, which rejected legitimate names such as `Transaction.StartPayment`, `PaymentPending` and the correlation ID `monkey-1` and turned those requests into 500 errors. `JsonLinesAuditStore` appends one JSON object per line to `audit.jsonl` with `FileMode.Append` under a per-file `SemaphoreSlim`; existing lines are never rewritten.

An audit failure before a mutation prevents that mutation. A failure to append the terminal event after business persistence propagates through global safe exception handling while leaving the preceding `Started` event available for reconciliation. It does not claim a complete audit. JSON cannot make separate business and audit collection writes atomic. A caller may receive an error even if a preceding business write completed; operators must reconcile unresolved `Started` records. This is the explicit failure policy available without a transactional database.

## Alternatives considered

1. Persist raw request payloads directly
   - Rejected because payloads can contain credentials and personal data.
2. Skip audit logging entirely
   - Rejected because it removes operational traceability.
3. Swallow audit-write failures to keep requests successful
   - Rejected because that silently represents an unaudited operation as complete.
4. Introduce a database for audit persistence
   - Deferred to preserve the approved JSON-only architecture.

## Consequences

- Application-level events and denied access can be correlated without storing hazardous values.
- Unsafe or non-allowlisted metadata is rejected rather than silently persisted.
- A Started record can require reconciliation if a later persistence step fails.
- JSON Lines provides process-local serialized appends, but no tamper resistance, cross-process coordination, or atomic business-plus-audit transactions.
- The local file is not a replacement for centralized protected audit retention or a SIEM.

## Related decisions

- ADR-003: JSON persistence model
- ADR-005: Authentication and role-based authorization
