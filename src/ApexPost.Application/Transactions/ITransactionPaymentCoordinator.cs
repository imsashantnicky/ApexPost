using ApexPost.Application.Persistence;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Transactions;

/// <summary>
/// Atomically persists a payment-pending transaction and records its idempotency key.
/// </summary>
public interface ITransactionPaymentCoordinator
{
    Task<Versioned<RetailTransaction>?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task<PaymentStartWriteResult> TryStartPaymentAsync(RetailTransaction transaction, long expectedVersion, string idempotencyKey, CancellationToken cancellationToken);
}

public sealed record PaymentStartWriteResult(Versioned<RetailTransaction>? Transaction, bool IsDuplicate);
