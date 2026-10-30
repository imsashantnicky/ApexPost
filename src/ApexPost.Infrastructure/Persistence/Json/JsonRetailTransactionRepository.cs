using ApexPost.Application.Persistence;
using ApexPost.Application.Transactions;
using ApexPost.Domain.Common;
using ApexPost.Domain.Payments;
using ApexPost.Domain.Transactions;

namespace ApexPost.Infrastructure.Persistence.Json;

public sealed class JsonRetailTransactionRepository : IRetailTransactionRepository, ITransactionPaymentCoordinator
{
    private readonly JsonCollectionStore<Data> store;

    public JsonRetailTransactionRepository(JsonDataOptions options) => store = new(options.DataDirectory, "transactions.json");

    public async Task<IReadOnlyList<Versioned<RetailTransaction>>> ListAsync(CancellationToken cancellationToken) =>
        (await store.ReadAllAsync(cancellationToken)).Values.Select(ToDomain).ToArray();

    public async Task<Versioned<RetailTransaction>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken) =>
        (await store.ReadAllAsync(cancellationToken)).TryGetValue(id.Value.ToString("N"), out var data) ? ToDomain(data) : null;

    public async Task<Versioned<RetailTransaction>?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var data = (await store.ReadAllAsync(cancellationToken)).Values.FirstOrDefault(item => string.Equals(item.PaymentIdempotencyKey, idempotencyKey, StringComparison.Ordinal));
        return data is null ? null : ToDomain(data);
    }

    public Task<PaymentStartWriteResult> TryStartPaymentAsync(RetailTransaction transaction, long expectedVersion, string idempotencyKey, CancellationToken cancellationToken) =>
        store.ExecuteAtomicAsync(items =>
        {
            var duplicate = items.Values.FirstOrDefault(item => string.Equals(item.PaymentIdempotencyKey, idempotencyKey, StringComparison.Ordinal));
            if (duplicate is not null)
                return (false, new PaymentStartWriteResult(ToDomain(duplicate), true));

            var key = transaction.Id.Value.ToString("N");
            if (!items.TryGetValue(key, out var current) || current.Version != expectedVersion)
                return (false, new PaymentStartWriteResult(null, false));

            var updated = ToData(transaction, expectedVersion + 1) with { PaymentIdempotencyKey = idempotencyKey };
            items[key] = updated;
            return (true, new PaymentStartWriteResult(ToDomain(updated), false));
        }, cancellationToken);

    public Task<bool> TryAddAsync(RetailTransaction transaction, CancellationToken cancellationToken) =>
        store.TryAddUniqueAsync(transaction.Id.Value.ToString("N"), ToData(transaction, 1),
            x => x.CounterReference == transaction.CounterReference && x.BranchId == transaction.BranchId.Value, cancellationToken);

    public Task<bool> TryUpdateAsync(RetailTransaction transaction, long expectedVersion, CancellationToken cancellationToken) =>
        store.ExecuteAtomicAsync(items =>
        {
            var key = transaction.Id.Value.ToString("N");
            if (!items.TryGetValue(key, out var current) || current.Version != expectedVersion) return (false, false);
            items[key] = ToData(transaction, expectedVersion + 1) with { PaymentIdempotencyKey = current.PaymentIdempotencyKey };
            return (true, true);
        }, cancellationToken);

    private static Versioned<RetailTransaction> ToDomain(Data x)
    {
        try
        {
            var transaction = RetailTransaction.Restore(EntityId.From(x.Id), EntityId.From(x.BranchId), x.CounterReference, x.CurrencyCode, x.CreatedAtUtc, x.Status, x.VoidReason,
                x.Lines.Select(item => new TransactionLineState(EntityId.From(item.Id), EntityId.From(item.ServiceId), item.Description, item.Quantity, item.UnitPrice, item.Kind)),
                x.Payments.Select(payment => new PaymentState(EntityId.From(payment.Id), payment.Amount, payment.CurrencyCode, payment.Method, payment.Reference, payment.Status, payment.DeclineReason, payment.ReversalReason)));
            return new Versioned<RetailTransaction>(transaction, x.Version);
        }
        catch (DomainException exception)
        {
            throw new JsonPersistenceException($"Persisted transaction '{x.Id:D}' is inconsistent and could not be restored.", exception);
        }
    }

    private static Data ToData(RetailTransaction transaction, long version) => new(
        transaction.Id.Value,
        version,
        transaction.BranchId.Value,
        transaction.CounterReference,
        transaction.CurrencyCode,
        transaction.CreatedAtUtc,
        transaction.Status,
        transaction.VoidReason,
        transaction.LineItems.Select(item => new LineData(item.Id.Value, item.ServiceId.Value, item.Description, item.Quantity, item.UnitPrice, item.Kind)).ToArray(),
        transaction.Payments.Select(payment => new PaymentData(payment.Id.Value, payment.Amount, payment.CurrencyCode, payment.Method, payment.Reference, payment.Status, payment.ReversalReason, payment.DeclineReason)).ToArray());

    private sealed record Data(Guid Id, long Version, Guid BranchId, string CounterReference, string CurrencyCode, DateTimeOffset? CreatedAtUtc, TransactionStatus Status, string? VoidReason, LineData[] Lines, PaymentData[] Payments, string? PaymentIdempotencyKey = null);
    private sealed record LineData(Guid Id, Guid ServiceId, string Description, int Quantity, decimal UnitPrice, TransactionLineKind Kind = TransactionLineKind.Service);
    private sealed record PaymentData(Guid Id, decimal Amount, string CurrencyCode, PaymentMethod Method, string Reference, PaymentStatus Status, string? ReversalReason = null, string? DeclineReason = null);
}
