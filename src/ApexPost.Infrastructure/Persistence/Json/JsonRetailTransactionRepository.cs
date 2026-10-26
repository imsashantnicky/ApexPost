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

    public async Task<bool> TryAddAsync(RetailTransaction transaction, CancellationToken cancellationToken)
    {
        var all = await store.ReadAllAsync(cancellationToken);
        if (all.Values.Any(x => x.CounterReference == transaction.CounterReference && x.BranchId == transaction.BranchId.Value)) return false;
        return await store.TryAddAsync(transaction.Id.Value.ToString("N"), ToData(transaction, 1), cancellationToken);
    }

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
        var transaction = new RetailTransaction(EntityId.From(x.Id), EntityId.From(x.BranchId), x.CounterReference, x.CurrencyCode, x.CreatedAtUtc);
        foreach (var item in x.Lines)
        {
            transaction.AddLineItem(EntityId.From(item.ServiceId), item.Description, item.Quantity, item.UnitPrice, item.Kind);
        }

        foreach (var payment in x.Payments)
        {
            var record = payment.Status switch
            {
                PaymentStatus.Pending => transaction.StartPayment(payment.Amount, payment.Method, payment.Reference),
                PaymentStatus.Confirmed or PaymentStatus.Reversed => transaction.StartPayment(payment.Amount, payment.Method, payment.Reference),
                PaymentStatus.Declined => transaction.StartPayment(payment.Amount, payment.Method, payment.Reference),
                PaymentStatus.Cancelled => transaction.StartPayment(payment.Amount, payment.Method, payment.Reference),
                _ => throw new InvalidOperationException("Persisted payment status is not supported.")
            };

            if (payment.Status is PaymentStatus.Confirmed or PaymentStatus.Reversed)
            {
                transaction.ConfirmPayment();
            }
            else if (payment.Status == PaymentStatus.Declined)
            {
                transaction.DeclinePayment(payment.DeclineReason ?? "Legacy decline reason unavailable");
            }
            else if (payment.Status == PaymentStatus.Cancelled)
            {
                transaction.Cancel(x.VoidReason ?? "Legacy void reason unavailable");
            }
        }

        if (x.Status == TransactionStatus.Reversed) transaction.ReversePayment(x.Payments.FirstOrDefault(p => p.Status == PaymentStatus.Reversed)?.ReversalReason ?? "Legacy reversal reason unavailable");
        if (x.Status == TransactionStatus.Cancelled && transaction.Status != TransactionStatus.Cancelled) transaction.Cancel(x.VoidReason ?? "Legacy void reason unavailable");
        if (x.Status != transaction.Status) throw new InvalidOperationException("Persisted transaction status is inconsistent with its payment history.");

        return new Versioned<RetailTransaction>(transaction, x.Version);
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
