# ADR-004: Caching strategy

- Status: Proposed
- Related files: current implementation has no dedicated cache layer

## Context

The repository currently uses direct repository reads for branch, inventory, money order, shipment, and transaction workflows. The file-backed design is intentionally simple and not yet burdened by a distributed caching layer.

## Decision

No production cache is adopted in the current implementation. The application reads data directly from repositories and keeps cached state out of the runtime model.

## Alternatives considered

1. Distributed memory cache
   - possible but adds stale-data and invalidation complexity
2. Browser-side caching of operational records
   - rejected because the app intentionally limits browser storage to safe local drafts and queue metadata only
3. Introduce caching before performance data justifies it
   - preferred over speculative optimization

## Consequences

- The application is straightforward and easy to reason about.
- The design remains consistent with the JSON data model.
- Performance will be dominated by direct repository reads and file operations.

## Security and operational implications

The absence of a shared cache reduces stale-data risk and eliminates cache invalidation as a cross-cutting operational concern. This is a tradeoff consistent with the current single-instance, local deployment model.

## Related decisions

- ADR-003: JSON persistence model
- ADR-005: Authentication and role-based authorization
