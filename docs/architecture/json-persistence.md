# JSON persistence

`JsonCollectionStore<T>` stores one keyed collection in one configured JSON file. Configure `ApexPost:JsonData:DataDirectory`; the default is `App_Data` relative to the hosting application. Repository adapters will map capabilities to separate files, such as `branches.json`, `staff.json`, `catalogue.json`, `inventory.json`, `shipments.json`, `money-orders.json`, and `transactions.json`.

Reads and mutations use an asynchronous, process-local lock per resolved file path. Mutations serialize to a temporary file in the same directory and replace the destination only after the temporary file is complete. Missing and empty files read as empty collections. Malformed JSON raises `JsonPersistenceException`; it is never reset or overwritten automatically.

This is appropriate only for one application process. The lock cannot coordinate two processes, containers, or deployment instances that share a directory. Run one instance per data directory. A database-backed repository is required before multi-instance deployment, cross-instance concurrency, or high-write workloads. JSON repository adapters also require explicit domain rehydration mappings so aggregate statuses and histories are faithfully restored; do not deserialize mutable aggregates directly and assume their lifecycle state survives.

## Reliability and audit

Writes use a process-local lock and same-directory temporary-file replacement. This prevents lost writes between threads in one process and avoids publishing a partly written JSON document. It does not provide transactional changes across files, process-to-process locking, durability guarantees after hardware failure, or automatic recovery.

`JsonAuditWriter` records action, entity type/id, optional actor id, timestamp, and safe operational metadata. It rejects metadata keys associated with passwords, tokens, secrets, card data, identity documents, addresses, phones, and email addresses. Audit writes are separate from business-file writes, so a future multi-file business operation needs an explicit coordination policy.

`JsonBackupRecovery` creates a `.bak` copy on explicit request. Restore is also explicit, validates that the backup is valid JSON, and replaces the target only after validation. The application never automatically restores a backup or silently replaces corrupt production data.
