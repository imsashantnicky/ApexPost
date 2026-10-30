# JSON persistence

`JsonCollectionStore<T>` stores one keyed collection in one configured JSON file. Configure `ApexPost:JsonData:DataDirectory`; the default is `App_Data` relative to the hosting application. Repository adapters will map capabilities to separate files, such as `branches.json`, `staff.json`, `catalogue.json`, `inventory.json`, `shipments.json`, `money-orders.json`, and `transactions.json`.

Reads and mutations use an asynchronous, process-local lock per resolved file path. Mutations serialize to a temporary file in the same directory and replace the destination only after the temporary file is complete. Missing and empty files read as empty collections. Malformed JSON raises `JsonPersistenceException`; it is never reset or overwritten automatically.

This is appropriate only for one application process. The lock cannot coordinate two processes, containers, or deployment instances that share a directory. Run one instance per data directory. A database-backed repository is required before multi-instance deployment, cross-instance concurrency, or high-write workloads. JSON repository adapters also require explicit domain rehydration mappings so aggregate statuses and histories are faithfully restored; do not deserialize mutable aggregates directly and assume their lifecycle state survives.

## Reliability and audit

Writes use a process-local lock and same-directory temporary-file replacement. This prevents lost writes between threads in one process and avoids publishing a partly written JSON document. It does not provide transactional changes across files, process-to-process locking, durability guarantees after hardware failure, or automatic recovery.

`JsonLinesAuditStore` (behind `AuditTrail` and the `IAuditStore` port) appends one JSON object per line to `audit.jsonl` with a `TimeProvider` UTC timestamp, actor/staff ID and role when resolved, branch, operation, target, outcome, correlation ID, and allowlisted safe metadata. Sensitive application mutations write `Started` before business persistence and a terminal outcome afterward. Audit-write failures propagate; a terminal-event failure after a business write leaves a `Started` event that requires reconciliation. The audit file is opened only with `FileMode.Append` under a process-local lock, so earlier lines are never rewritten; it is not tamper-resistant and cannot coordinate across processes. Audit and business files cannot be committed atomically together, so future financial workflows requiring guaranteed atomicity need a transactional store.

`JsonBackupRecovery` creates a `.bak` copy on explicit request. Restore is also explicit, validates that the backup is valid JSON, and replaces the target only after validation. The application never automatically restores a backup or silently replaces corrupt production data.
