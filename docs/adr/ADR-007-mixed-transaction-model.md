# ADR-007: Mixed transaction and payment model

- Status: Accepted
- Related files: `src/ApexPost.Application/Transactions`, `src/ApexPost.Domain/Transactions`, `src/ApexPost.Application/Reporting/OperationalReportService.cs`

## Context

ApexPost includes retail transactions and payment records. These are conceptually related but not interchangeable: the system needs to represent transaction totals, payment statuses, pending payment states, and reporting without overclaiming success for a local operation.

## Decision

The implementation models a `RetailTransaction` aggregate with a collection of `PaymentRecord` entries and explicit transaction statuses such as `Open`, `PaymentPending`, `Paid`, `Cancelled`, and `Reversed`. The reporting service aggregates these values into operational summaries without conflating pending local activity with confirmed server state.

The design preserves the distinction between locally queued or pending actions and confirmed records, which is important for offline draft behavior and operational clarity.

## Alternatives considered

1. Store only final totals and drop payment detail
   - rejected because it makes reconciliation and reporting less reliable
2. Treat all local client actions as server-confirmed
   - rejected because the project purposefully keeps pending local queue entries distinct from confirmed data
3. Split payment and transaction lifecycle into unrelated records
   - rejected because the aggregate model is the natural ownership boundary for payment state

## Consequences

- Reporting reflects both transaction totals and payment totals.
- Payment state is auditable and observable.
- The model remains intentionally simple and file-backed rather than a full accounting system.

## Security and operational implications

The transaction model prevents the UI from incorrectly presenting local or pending workflow as server-confirmed work. This improves operator trust and reduces false operational reporting.

## Related decisions

- ADR-003: JSON persistence model
- ADR-006: Audit redaction and operational logging
- `src/ApexPost.Application/Reporting/OperationalReportService.cs`
