using ApexPost.Application.MoneyOrders;
using ApexPost.Application.Persistence;
using ApexPost.Domain.Common;
using ApexPost.Domain.MoneyOrders;

namespace ApexPost.Infrastructure.Persistence.Json;
public sealed class JsonMoneyOrderRepository : IMoneyOrderRepository
{
    private readonly JsonCollectionStore<Data> store;
    public JsonMoneyOrderRepository(JsonDataOptions options) => store = new(options.DataDirectory, "money-orders.json");
    public async Task<IReadOnlyList<Versioned<MoneyOrder>>> ListAsync(CancellationToken cancellationToken) => (await store.ReadAllAsync(cancellationToken)).Values.Select(ToDomain).ToArray();
    public async Task<Versioned<MoneyOrder>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken) => (await store.ReadAllAsync(cancellationToken)).TryGetValue(id.Value.ToString("N"), out var x) ? ToDomain(x) : null;
    public Task<bool> TryAddAsync(MoneyOrder moneyOrder, CancellationToken cancellationToken) => store.TryAddUniqueAsync(moneyOrder.Id.Value.ToString("N"), ToData(moneyOrder, 1), x => x.Number == moneyOrder.OrderNumber.Value, cancellationToken);
    public Task<bool> TryUpdateAsync(MoneyOrder moneyOrder, long expectedVersion, CancellationToken cancellationToken) => store.TryReplaceAsync(moneyOrder.Id.Value.ToString("N"), ToData(moneyOrder, expectedVersion + 1), x => x.Version == expectedVersion, cancellationToken);
    private static Versioned<MoneyOrder> ToDomain(Data x) => new(MoneyOrder.Restore(EntityId.From(x.Id), MoneyOrderNumber.From(x.Number), new MoneyOrderAmount(x.Amount, x.Currency), EntityId.From(x.BranchId), new MoneyOrderBeneficiary(x.Beneficiary, x.Reference), x.Status, x.IdentityVerificationId is Guid verificationId ? EntityId.From(verificationId) : null), x.Version);
    private static Data ToData(MoneyOrder x, long version) => new(x.Id.Value, version, x.OrderNumber.Value, x.Amount.Amount, x.Amount.CurrencyCode, x.OriginatingBranchId.Value, x.Beneficiary.Name, x.Beneficiary.Reference, x.Status, x.IdentityVerificationId?.Value);
    private sealed record Data(Guid Id, long Version, string Number, decimal Amount, string Currency, Guid BranchId, string Beneficiary, string Reference, MoneyOrderStatus Status, Guid? IdentityVerificationId = null);
}
