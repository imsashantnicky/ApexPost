# Changelog

All notable changes to ApexPost. Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Fixed

- **Transactions reload after a declined payment.** `JsonRetailTransactionRepository` replayed history by calling `StartPayment` for every stored payment. After pay, decline, then add a line, the replay no longer matched the new total and threw, which broke that transaction, `ListAsync`, the Transactions page and reports. `RetailTransaction.Restore` now restores lines and payments with their persisted ids and statuses and validates the snapshot as a whole.
- **Admin cross-branch payment and identity operations returned 500.** The audit writer keyword-scanned the operation name, so `Transaction.StartPayment`, `PaymentConfirmed`, `RetryPayment` and `IdentityVerification.*` were rejected. Operation names are now validated with `^[A-Za-z]+(\.[A-Za-z]+)+$`.
- **Every MVC action returned 500 for some correlation IDs.** A valid inbound `X-Correlation-ID` such as `monkey-1` contains `key` and failed the keyword scan. Correlation IDs are now checked by the `CorrelationIdMiddleware` format rule instead.
- **Every successful payment start failed against the real audit writer.** Its `Status=PaymentPending` metadata contains `payment`. `Status` now accepts domain status names directly.
- **Seed data duplicated business keys.** Seeding de-duplicated only by GUID, so legacy rows under other ids (EMP-0001/EMP-0002, BLR-001/MYS-001, EXP-001/REG-001) were seeded again. Branches, staff and services are now matched by business key, and seeded references point at the stored ids.

### Changed

- **Audit store.** `IAuditStore` (Application) replaces `IAccessAuditWriter`. `AuditTrail` validates metadata and stamps entries with `TimeProvider`. `JsonLinesAuditStore` appends one JSON object per line to `audit.jsonl` (`FileMode.Append`, async, per-file `SemaphoreSlim`). See `AUDIT-SAMPLE.jsonl`.
- **MVC auditing** covers only state-changing requests and actions marked `[Audited]` (currently Reports), not GET page views. Each entry records staff, role, branch, controller, action, `id` route value, status code, duration and correlation ID.
- **List pages** (Inventory, Transactions, Shipments, Money orders) authorize the actor once and filter rows by branch scope (administrators see all). A list writes no audit entry when allowed and exactly one when denied; previously each other-branch row wrote one. The report service no longer runs a per-branch check for administrators.
- **Staff lookups** in `ValidatedStaffHandler` are cached for 30 s (`StaffAccessCache`, `IMemoryCache`) and invalidated immediately on staff update or deactivation.
- **Roles and policies.** One `Roles` class and one `StaffRoleMapping` replace role string literals and local role switches. A `Policies` class holds the authorization policy names.
- **Branch pickers.** `BranchSelectionService` replaces the duplicated `PopulateBranchesAsync`/`ApplyActorBranch` copies in four controllers. A branch user's own branch is offered only while it is active.
- **Time.** All clock reads go through `TimeProvider` (`TimeProvider.System` in the app, `FakeTimeProvider` in tests).
- **Repository adds with a business key** (branch code, employee number, service code, per-branch SKU, money-order number, per-branch counter reference) check and insert in one locked step. Inventory previously had no SKU check.
- **Build.** SDK pinned to `10.0.100` with `latestFeature` roll-forward, and `TreatWarningsAsErrors` is on. Test projects allow underscores in test names (CA1707 disabled under `tests/` only).
- **Configuration.** JSON seeding is disabled except in `appsettings.Development.json`. Empty `Integration:ApiKey`/`StaffId` placeholders were added; set real values only with user-secrets or environment variables.

### Added

- Money orders list page (`MoneyOrders/Index`).

### Removed

- Stray root `EmployeeNumber` copy, `Web/Class1.cs`, unused `IUseCase`, unused `SafeHealthCheck`, and the `LocalAccountService` overloads that created accounts without a staff link.
- `App_Data` from source control. It stays git-ignored and seed data comes from code. Delete an existing local `App_Data` folder to regenerate it without legacy duplicates.
- Hard-coded test passwords. Tests now generate them at runtime.

## Week 2 baseline

Shipped before this changelog was started:

- MVC `AuditActionFilter`, `ValidationActionFilter` and `PerformanceActionFilter`, registered globally.
- `SecurityHeadersMiddleware` with a restrictive baseline CSP, `CorrelationIdMiddleware` (accepts only short ASCII letters, digits, `-`, `_` and `.`), maintenance mode, integration IP allow-listing, login and partner rate limits, and secure cookies outside Development.
- Integration API-key authentication for partner money-order routes.
- C# 14 extension members (`StringExtensions`) and a `field`-backed property (`MoneyOrderAmount.Amount`).
- `Started`/terminal audit events around sensitive mutations, plus authorization denials recorded at `OperationalAccess`.
- CI build and test on pushes and pull requests to `master` and `main`.

## Known limitations

- JSON storage is not tamper-proof, does not coordinate multiple processes, and cannot commit business and audit records atomically. A `Started` audit entry without a terminal entry needs manual reconciliation.
- No tax policy or tax rates are modelled, so transaction totals and reports exclude tax.
- The CSP is intentionally strict. Check views in a browser after adding inline scripts or third-party assets.
