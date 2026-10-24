# ADR-003: JSON file persistence for the current deployment model

- Status: Accepted
- Related files: `src/ApexPost.Infrastructure/Persistence/Json`, `src/ApexPost.Web/Program.cs`

## Context

The solution needs local persistence that is easy to back up, restore, and test. It must also fail loudly when data becomes malformed instead of silently resetting state.

## Decision

The repository uses a file-backed JSON strategy built around `JsonCollectionStore<T>`. That implementation:

- stores each collection in a single JSON file
- uses a process-local lock for thread safety within one app instance
- writes to a temporary file and swaps it into place atomically
- throws `JsonPersistenceException` when JSON is malformed or file I/O fails

This model is configured via `ApexPost:JsonData:DataDirectory` and optional seeding via `ApexPost:JsonSeed:Enabled`.

## Alternatives considered

1. SQLite or EF Core immediately
   - rejected to keep the initial implementation simple and restore-friendly
2. In-memory persistence only
   - rejected because data must survive restarts
3. Auto-healing malformed data files
   - rejected because silent recovery is unsafe

## Consequences

- The solution is easy to run and restore in a development environment.
- The configured data directory becomes operational state and requires backup discipline.
- Multi-instance or distributed deployment is not supported by the current design.

## Security and operational implications

This persistence strategy is explicit and safe for local internal use, but it is not a production-grade database. The app is designed to detect malformed files and stop instead of silently overwriting data.

## Related decisions

- ADR-001: Layered architecture
- ADR-006: Audit redaction and operational logging
- `docs/architecture/json-persistence.md`
