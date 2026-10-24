# ADR-006: Operational auditing with sensitive-field rejection

- Status: Accepted
- Related files: `src/ApexPost.Infrastructure/Persistence/Json/JsonAuditWriter.cs`

## Context

Operational systems need an audit trail for actions and state changes but must avoid capturing secrets, payment-data fields, tokens, identity document values, and authentication material. The JSON repository is intentionally lightweight and local, so the audit layer must reject dangerous metadata before writing.

## Decision

`JsonAuditWriter` records action, entity type, entity ID, optional actor ID, UTC timestamp, and a metadata dictionary. Before persisting metadata it rejects keys and values containing sensitive fragments such as:

- password
- token
- secret
- card
- credential
- payment
- identity
- address
- email
- phone
- session
- auth

These checks are enforced before writing a JSON audit record, preventing accidental leaks while preserving operational traceability.

## Alternatives considered

1. Persist raw request payloads directly
   - rejected because it would capture credentials and personal data
2. Skip audit logging entirely
   - rejected because it removes operational traceability for recovery and review
3. Store large unfiltered logs in a plaintext file
   - rejected because it introduces unnecessary data exposure risk

## Consequences

- Operational reviews remain useful without storing hazardous values.
- Some metadata is intentionally discarded when it contains sensitive fragments.
- The audit model is local and file-based rather than a full SIEM platform.

## Security and operational implications

The implementation improves operational safety while still keeping a useful local audit trail. It does not replace a dedicated enterprise logging or SIEM solution for larger deployments.

## Related decisions

- ADR-003: JSON persistence model
- ADR-005: Authentication and role-based authorization
