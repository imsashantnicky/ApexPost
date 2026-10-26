# ADR-006: Application-boundary auditing with safe failure semantics

- Status: Accepted
- Related files: `src/ApexPost.Application/Security/OperationalAccess.cs`, `src/ApexPost.Infrastructure/Persistence/Json/JsonAuditWriter.cs`, `src/ApexPost.Web/Filters/AuditActionFilter.cs`

## Context

Operational systems need an append-only audit trail for sensitive state changes and rejected access while avoiding secrets, payment data, identity-document values, authentication material, and free-form request content. MVC-only auditing is insufficient because application use cases can also run outside HTTP. JSON persistence remains the selected storage architecture.

## Decision

Use cases and `OperationalAccess` depend on `IAccessAuditWriter`; Infrastructure implements it with `JsonAuditWriter`. Records carry UTC timestamp, actor/staff ID and role when resolved, validated branch, operation, target, outcome, correlation ID, and allowlisted safe metadata. Sensitive mutations append `Started` before business persistence and a terminal event afterward. Authorization denials are recorded at the Application access boundary. The MVC `AuditActionFilter` records only controlled action name, method, status, elapsed time, and correlation ID; it propagates audit failures.

The writer accepts a small set of metadata keys and rejects unsupported keys or values containing sensitive fragments. The audit collection is written through `JsonCollectionStore<T>`, which serializes same-process writers and atomically replaces a complete JSON file via a temporary file. Malformed JSON is not auto-repaired; the write fails and the existing file is preserved.

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
- JSON provides process-local concurrency protection and replacement writes, but no tamper resistance, cross-process coordination, or atomic business-plus-audit transactions.
- The local file is not a replacement for centralized protected audit retention or a SIEM.

## Related decisions

- ADR-003: JSON persistence model
- ADR-005: Authentication and role-based authorization
