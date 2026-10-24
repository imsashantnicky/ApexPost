using ApexPost.Application.Persistence;
using ApexPost.Application.Transactions;
using ApexPost.Domain.Common;
using ApexPost.Domain.Payments;
using ApexPost.Domain.Transactions;

namespace ApexPost.Infrastructure.Persistence.Json;

public sealed class JsonRetailTransactionRepository : IRetailTransactionRepository
{
    private readonly JsonCollectionStore<Data> store;

    public JsonRetailTransactionRepository(JsonDataOptions options) => store = new(options.DataDirectory, "transactions.json");

    public async Task<IReadOnlyList<Versioned<RetailTransaction>>> ListAsync(CancellationToken cancellationToken) =>
        (await store.ReadAllAsync(cancellationToken)).Values.Select(ToDomain).ToArray();

    public async Task<Versioned<RetailTransaction>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken) =>
        (await store.ReadAllAsync(cancellationToken)).TryGetValue(id.Value.ToString("N"), out var data) ? ToDomain(data) : null;

    public async Task<bool> TryAddAsync(RetailTransaction transaction, CancellationToken cancellationToken)
    {
        var all = await store.ReadAllAsync(cancellationToken);
        if (all.Values.Any(x => x.CounterReference == transaction.CounterReference && x.BranchId == transaction.BranchId.Value)) return false;
        return await store.TryAddAsync(transaction.Id.Value.ToString("N"), ToData(transaction, 1), cancellationToken);
    }

    public Task<bool> TryUpdateAsync(RetailTransaction transaction, long expectedVersion, CancellationToken cancellationToken) =>
        store.TryReplaceAsync(transaction.Id.Value.ToString("N"), ToData(transaction, expectedVersion + 1), data => data.Version == expectedVersion, cancellationToken);

    private static Versioned<RetailTransaction> ToDomain(Data x)
    {
        var transaction = new RetailTransaction(EntityId.From(x.Id), EntityId.From(x.BranchId), x.CounterReference, x.CurrencyCode);
        foreach (var item in x.Lines)
        {
            transaction.AddLineItem(EntityId.From(item.ServiceId), item.Description, item.Quantity, item.UnitPrice);
        }

        foreach (var payment in x.Payments)
        {
            var record = payment.Status switch
            {
                PaymentStatus.Pending => transaction.StartPayment(payment.Amount, payment.Method, payment.Reference),
                PaymentStatus.Confirmed => transaction.StartPayment(payment.Amount, payment.Method, payment.Reference),
                PaymentStatus.Declined => throw new InvalidOperationException("Persisted declined payment data is not supported."),
                _ => throw new InvalidOperationException("Persisted payment status is not supported.")
            };

            if (payment.Status == PaymentStatus.Confirmed)
            {
                transaction.ConfirmPayment();
            }
        }

        if (x.Status == TransactionStatus.Paid) transaction.ConfirmPayment();
        if (x.Status == TransactionStatus.Reversed) { transaction.ConfirmPayment(); transaction.ReversePayment(); }
        if (x.Status == TransactionStatus.Cancelled) transaction.Cancel();

        return new Versioned<RetailTransaction>(transaction, x.Version);
    }

    private static Data ToData(RetailTransaction transaction, long version) => new(
        transaction.Id.Value,
        version,
        transaction.BranchId.Value,
        transaction.CounterReference,
        transaction.CurrencyCode,
        transaction.Status,
        transaction.LineItems.Select(item => new LineData(item.Id.Value, item.ServiceId.Value, item.Description, item.Quantity, item.UnitPrice)).ToArray(),
        transaction.Payments.Select(payment => new PaymentData(payment.Id.Value, payment.Amount, payment.CurrencyCode, payment.Method, payment.Reference, payment.Status)).ToArray());

    private sealed record Data(Guid Id, long Version, Guid BranchId, string CounterReference, string CurrencyCode, TransactionStatus Status, LineData[] Lines, PaymentData[] Payments);
    private sealed record LineData(Guid Id, Guid ServiceId, string Description, int Quantity, decimal UnitPrice);
    private sealed record PaymentData(Guid Id, decimal Amount, string CurrencyCode, PaymentMethod Method, string Reference, PaymentStatus Status);
}
