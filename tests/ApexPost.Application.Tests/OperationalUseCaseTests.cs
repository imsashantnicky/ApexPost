using ApexPost.Application.Inventory;
using ApexPost.Application.MoneyOrders;
using ApexPost.Application.Persistence;
using ApexPost.Application.Shipping;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.MoneyOrders;
using ApexPost.Domain.Shipping;

namespace ApexPost.Application.Tests;

public class OperationalUseCaseTests
{
    [Fact]
    public async Task InventoryReceiveIssueAndAdjustmentUseVersionedUpdates()
    {
        var repository = new InventoryRepositoryDouble(Item());
        var useCase = new InventoryOperationsUseCase(repository);
        var itemId = repository.ItemId;

        var received = await useCase.ReceiveAsync(new(itemId, 1, 10, "Supplier delivery", DateTimeOffset.UtcNow), default);
        var issued = await useCase.IssueAsync(new(itemId, 2, 4, "Counter sale", DateTimeOffset.UtcNow), default);
        var adjusted = await useCase.AdjustToAsync(new(itemId, 3, 8, "Stock count", DateTimeOffset.UtcNow), default);

        Assert.Equal(10, received.Value.QuantityOnHand);
        Assert.Equal(6, issued.Value.QuantityOnHand);
        Assert.Equal(8, adjusted.Value.QuantityOnHand);
    }

    [Fact]
    public async Task InventoryRejectsStaleVersionAndUnavailableItem()
    {
        var repository = new InventoryRepositoryDouble(Item());
        var useCase = new InventoryOperationsUseCase(repository);

        var stale = await useCase.ReceiveAsync(new(repository.ItemId, 2, 1, "Delivery", DateTimeOffset.UtcNow), default);
        var missing = await useCase.ReceiveAsync(new(Guid.NewGuid(), 1, 1, "Delivery", DateTimeOffset.UtcNow), default);

        Assert.Equal("concurrency.conflict", Assert.Single(stale.Errors).Code);
        Assert.Equal("resource.not-found", Assert.Single(missing.Errors).Code);
    }

    [Fact]
    public async Task InventoryMapsInvalidIssueToDomainFailure()
    {
        var repository = new InventoryRepositoryDouble(Item());
        var result = await new InventoryOperationsUseCase(repository)
            .IssueAsync(new(repository.ItemId, 1, 1, "Counter sale", DateTimeOffset.UtcNow), default);

        Assert.Equal("domain.rule", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task ShipmentBookingAndTrackingFollowLifecycle()
    {
        var repository = new ShipmentRepositoryDouble();
        var useCase = new ShipmentOperationsUseCase(repository, new PriceLookup());
        var booked = await useCase.BookAsync(ShipmentRequest("SHP-000001"), default);
        var accepted = await useCase.AcceptAsync(booked.Value.Id, 1, default);
        var dispatched = await useCase.DispatchAsync(booked.Value.Id, 2, default);
        var delivered = await useCase.DeliverAsync(booked.Value.Id, 3, default);

        Assert.Equal(ShipmentStatus.Delivered, delivered.Value.Status);
        Assert.Equal(4, delivered.Value.Version);
    }

    [Fact]
    public async Task ShipmentRejectsDuplicateBookingAndInvalidTransition()
    {
        var useCase = new ShipmentOperationsUseCase(new ShipmentRepositoryDouble(), new PriceLookup());
        var booked = await useCase.BookAsync(ShipmentRequest("SHP-000001"), default);
        var duplicate = await useCase.BookAsync(ShipmentRequest("shp-000001"), default);
        var invalid = await useCase.DeliverAsync(booked.Value.Id, 1, default);

        Assert.Equal("identifier.duplicate", Assert.Single(duplicate.Errors).Code);
        Assert.Equal("domain.rule", Assert.Single(invalid.Errors).Code);
    }

    [Fact]
    public async Task ShipmentBookingRejectsInvalidRequest()
    {
        var request = ShipmentRequest("SHP-000001") with { ChargeAmount = 0m };
        var result = await new ShipmentOperationsUseCase(new ShipmentRepositoryDouble(), new PriceLookup()).BookAsync(request, default);

        Assert.Equal("domain.rule", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task MoneyOrderIssuanceAndLifecycleFollowDomainRules()
    {
        var repository = new MoneyOrderRepositoryDouble();
        var useCase = new MoneyOrderOperationsUseCase(repository);
        var issued = await useCase.IssueAsync(MoneyOrderRequest("MO-000001"), default);
        var paid = await useCase.MarkPaidAsync(issued.Value.Id, 1, default);

        Assert.Equal(MoneyOrderStatus.Paid, paid.Value.Status);
        Assert.Equal(2, paid.Value.Version);
    }

    [Fact]
    public async Task MoneyOrderRejectsDuplicateAndInvalidTerminalTransition()
    {
        var useCase = new MoneyOrderOperationsUseCase(new MoneyOrderRepositoryDouble());
        var issued = await useCase.IssueAsync(MoneyOrderRequest("MO-000001"), default);
        var duplicate = await useCase.IssueAsync(MoneyOrderRequest("mo-000001"), default);
        await useCase.MarkPaidAsync(issued.Value.Id, 1, default);
        var cancel = await useCase.CancelAsync(issued.Value.Id, 2, default);

        Assert.Equal("identifier.duplicate", Assert.Single(duplicate.Errors).Code);
        Assert.Equal("domain.rule", Assert.Single(cancel.Errors).Code);
    }

    [Fact]
    public async Task MoneyOrderIssuanceValidatesAmountAndMissingResource()
    {
        var useCase = new MoneyOrderOperationsUseCase(new MoneyOrderRepositoryDouble());
        var invalid = await useCase.IssueAsync(MoneyOrderRequest("MO-000001") with { Amount = 0m }, default);
        var missing = await useCase.GetAsync(Guid.NewGuid(), default);

        Assert.Equal("domain.rule", Assert.Single(invalid.Errors).Code);
        Assert.Equal("resource.not-found", Assert.Single(missing.Errors).Code);
    }

    private static InventoryItem Item() => new(EntityId.New(), EntityId.New(), InventorySku.From("ENV-001"), "Envelope", 2);
    private static BookShipmentRequest ShipmentRequest(string number) => new(number, "Asha Rao", "1 Market Road", "Bengaluru", "560001", "IN", "Rahul Verma", "2 Lake Road", "Mysuru", "570001", "IN", Guid.NewGuid(), 1m, "Documents", 10m, "INR");
    private static IssueMoneyOrderRequest MoneyOrderRequest(string number) => new(number, 100m, "INR", Guid.NewGuid(), "Asha Rao", "BEN-1");

    private sealed class InventoryRepositoryDouble : IInventoryItemRepository
    {
        private Versioned<InventoryItem> item;
        public InventoryRepositoryDouble(InventoryItem item) { this.item = new(item, 1); ItemId = item.Id.Value; }
        public Guid ItemId { get; }
        public Task<IReadOnlyList<Versioned<InventoryItem>>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Versioned<InventoryItem>>>([item]);
        public Task<Versioned<InventoryItem>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken) => Task.FromResult<Versioned<InventoryItem>?>(id.Value == ItemId ? item : null);
        public Task<bool> TryAddAsync(InventoryItem value, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(InventoryItem value, long expectedVersion, CancellationToken cancellationToken)
        {
            if (expectedVersion != item.Version) return Task.FromResult(false);
            item = new(value, expectedVersion + 1); return Task.FromResult(true);
        }
    }

    private sealed class ShipmentRepositoryDouble : IShipmentRepository
    {
        private readonly Dictionary<Guid, Versioned<Shipment>> items = [];
        public Task<IReadOnlyList<Versioned<Shipment>>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Versioned<Shipment>>>(items.Values.ToArray());
        public Task<Versioned<Shipment>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken) => Task.FromResult(items.GetValueOrDefault(id.Value));
        public Task<bool> TryAddAsync(Shipment item, CancellationToken cancellationToken) { if (items.Values.Any(value => value.Value.ShipmentNumber == item.ShipmentNumber)) return Task.FromResult(false); items[item.Id.Value] = new(item, 1); return Task.FromResult(true); }
        public Task<bool> TryUpdateAsync(Shipment item, long expectedVersion, CancellationToken cancellationToken) { if (!items.TryGetValue(item.Id.Value, out var current) || current.Version != expectedVersion) return Task.FromResult(false); items[item.Id.Value] = new(item, expectedVersion + 1); return Task.FromResult(true); }
    }

    private sealed class MoneyOrderRepositoryDouble : IMoneyOrderRepository
    {
        private readonly Dictionary<Guid, Versioned<MoneyOrder>> items = [];
        public Task<Versioned<MoneyOrder>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken) => Task.FromResult(items.GetValueOrDefault(id.Value));
        public Task<bool> TryAddAsync(MoneyOrder item, CancellationToken cancellationToken) { if (items.Values.Any(value => value.Value.OrderNumber == item.OrderNumber)) return Task.FromResult(false); items[item.Id.Value] = new(item, 1); return Task.FromResult(true); }
        public Task<bool> TryUpdateAsync(MoneyOrder item, long expectedVersion, CancellationToken cancellationToken) { if (!items.TryGetValue(item.Id.Value, out var current) || current.Version != expectedVersion) return Task.FromResult(false); items[item.Id.Value] = new(item, expectedVersion + 1); return Task.FromResult(true); }
    }

    private sealed class PriceLookup : ApexPost.Application.Transactions.IServicePriceLookup
    {
        public Task<ApexPost.Domain.Pricing.ServicePrice?> GetCurrentPriceAsync(EntityId serviceId, string currencyCode, CancellationToken cancellationToken)
        {
            return Task.FromResult<ApexPost.Domain.Pricing.ServicePrice?>(ApexPost.Domain.Pricing.ServicePrice.From(10m, currencyCode));
        }
    }
}
