# Application layer conventions

## Scope

Application coordinates use cases and depends only on Domain. It owns request and response DTOs, validation orchestration, use-case interfaces, and ports required by a concrete use case. It does not reference ASP.NET Core, MVC types, JSON files, repository implementations, or other Infrastructure details.

## Use cases and DTOs

Create one use case per business operation. Use immutable `sealed record` request and response DTOs in the capability folder that owns the operation. Use descriptive names such as `CreateBranchRequest`, `CreateBranchResponse`, and `CreateBranchUseCase`. MVC binding/view models remain in Web and are mapped at the controller boundary.

`IUseCase<TRequest, TResponse>` is the common execution contract. Its `ExecuteAsync` method always accepts a `CancellationToken`; use cases must pass it to repository and integration calls. Do not add asynchronous I/O to Domain entities.

## Results and validation

Use `ApplicationResult<T>` for a use case that returns data and `ApplicationResult` for a command with no response payload. Create generic results through `ApplicationResults.Success` and `ApplicationResults.Failure`. Successful results have no validation errors. Failed results require at least one `ValidationError`; they cannot expose a response value. `ValidationError` contains a stable code, user-safe message, and optional field name. Controllers translate these outcomes into the appropriate HTTP response and validation summary.

Use Domain exceptions only for a broken domain invariant. Application catches and maps expected domain-rule rejections into stable application failures at the use-case boundary. It must not disclose exception internals to callers.

## Ports and dependencies

Repository ports now exist for Branches, Staff, and Catalogue because their management use cases read and write aggregates. Each exposes asynchronous `GetByIdAsync`, atomic `TryAddAsync`, and compare-and-swap `TryUpdateAsync` operations, all with a `CancellationToken`. `TryAddAsync` must enforce the capability's business identifier uniqueness atomically; `TryUpdateAsync` must update only when the supplied expected version matches. Current-user, clock, and unit-of-work ports remain deferred because no implemented use case needs them.

When a use case eventually changes multiple persisted aggregates, its unit-of-work boundary belongs in Application and its implementation belongs in Infrastructure. Single-aggregate operations should not gain a unit-of-work dependency merely by convention.

## Financial operation idempotency

Starting a counter payment requires a caller-supplied, non-empty idempotency key. `ITransactionPaymentCoordinator` must atomically bind that key to the transaction and persist its payment-pending state with the expected version. A retry with the same key and transaction returns the existing transaction result; reuse of a key for another transaction is rejected. The coordinator must not write a key if the transaction update fails. This is a local counter-record guarantee and does not indicate external-provider approval.
