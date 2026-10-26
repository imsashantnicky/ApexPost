using ApexPost.Application.Inventory;
using ApexPost.Application.IdentityVerification;
using ApexPost.Application.MoneyOrders;
using ApexPost.Application.Persistence;
using ApexPost.Application.Shipping;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.MoneyOrders;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.IdentityVerification;

namespace ApexPost.Application.Tests;

public class OperationalUseCaseTests
{
    [Fact]
    public async Task InventoryReceiveIssueAndAdjustmentUseVersionedUpdates()
    {
        var repository = new InventoryRepositoryDouble(Item());
        var useCase = new InventoryOperationsUseCase(repository, OperationalAccessFixture.Create());
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
        var useCase = new InventoryOperationsUseCase(repository, OperationalAccessFixture.Create());

        var stale = await useCase.ReceiveAsync(new(repository.ItemId, 2, 1, "Delivery", DateTimeOffset.UtcNow), default);
        var missing = await useCase.ReceiveAsync(new(Guid.NewGuid(), 1, 1, "Delivery", DateTimeOffset.UtcNow), default);

        Assert.Equal("concurrency.conflict", Assert.Single(stale.Errors).Code);
        Assert.Equal("resource.not-found", Assert.Single(missing.Errors).Code);
    }

    [Fact]
    public async Task InventoryMapsInvalidIssueToDomainFailure()
    {
        var repository = new InventoryRepositoryDouble(Item());
        var result = await new InventoryOperationsUseCase(repository, OperationalAccessFixture.Create())
            .IssueAsync(new(repository.ItemId, 1, 1, "Counter sale", DateTimeOffset.UtcNow), default);

        Assert.Equal("domain.rule", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task InventoryMutationDoesNotProceedWhenStartedAuditCannotBeWritten()
    {
        var repository = new InventoryRepositoryDouble(Item());
        var access = OperationalAccessFixture.Create(auditWriter: new FailingAuditWriter());
        var useCase = new InventoryOperationsUseCase(repository, access);

        await Assert.ThrowsAsync<IOException>(() => useCase.ReceiveAsync(new(repository.ItemId, 1, 5, "synthetic receipt", DateTimeOffset.UtcNow), default));

        var item = await repository.GetByIdAsync(EntityId.From(repository.ItemId), default);
        Assert.Equal(0, item!.Value.QuantityOnHand);
        Assert.Equal(1, item.Version);
    }

    [Fact]
    public async Task ShipmentBookingAndTrackingFollowLifecycle()
    {
        var repository = new ShipmentRepositoryDouble();
        var useCase = NewShipmentUseCase(repository);
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
        var useCase = NewShipmentUseCase(new ShipmentRepositoryDouble());
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
    var result = await NewShipmentUseCase(new ShipmentRepositoryDouble())
        .BookAsync(request, default);

    Assert.Equal("charge.invalid", Assert.Single(result.Errors).Code);
}

    [Fact]
    public async Task ShipmentBookingUsesTheConfiguredZoneAndServiceLevelRate()
    {
        var missingRate = await NewShipmentUseCase(new ShipmentRepositoryDouble()).BookAsync(
            ShipmentRequest("SHP-000020") with { ZoneCode = "Z99" }, default);

        Assert.Equal("shipping.rate.unavailable", Assert.Single(missingRate.Errors).Code);
    }

    [Fact]
    public async Task ShipmentBookingEnforcesConfiguredMaximumWeightIncludingBoundary()
    {
        var useCase = NewShipmentUseCase(new ShipmentRepositoryDouble(), maximumWeight: 2m);
        var boundary = await useCase.BookAsync(ShipmentRequest("SHP-000010") with { WeightKilograms = 2m }, default);
        var over = await useCase.BookAsync(ShipmentRequest("SHP-000011") with { WeightKilograms = 2.001m }, default);
        Assert.True(boundary.IsSuccess);
        Assert.Equal("domain.rule", Assert.Single(over.Errors).Code);
    }

    [Fact]
    public async Task MoneyOrderIssuanceAndLifecycleFollowDomainRules()
    {
        var repository = new MoneyOrderRepositoryDouble();
        var useCase = NewMoneyOrderUseCase(repository);
        var issued = await useCase.IssueAsync(MoneyOrderRequest("MO-000001"), default);
        var paid = await useCase.MarkPaidAsync(issued.Value.Id, 1, default);

        Assert.Equal(MoneyOrderStatus.Paid, paid.Value.Status);
        Assert.Equal(2, paid.Value.Version);
    }

    [Fact]
    public async Task MoneyOrderRejectsDuplicateAndInvalidTerminalTransition()
    {
        var useCase = NewMoneyOrderUseCase(new MoneyOrderRepositoryDouble());
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
        var useCase = NewMoneyOrderUseCase(new MoneyOrderRepositoryDouble());
        var invalid = await useCase.IssueAsync(MoneyOrderRequest("MO-000001") with { Amount = 0m }, default);
        var missing = await useCase.GetAsync(Guid.NewGuid(), default);

        Assert.Equal("domain.rule", Assert.Single(invalid.Errors).Code);
        Assert.Equal("resource.not-found", Assert.Single(missing.Errors).Code);
    }

    private static InventoryItem Item() => new(EntityId.New(), EntityId.New(), InventorySku.From("ENV-001"), "Envelope", 2);
    private sealed class FailingAuditWriter : ApexPost.Application.Security.IAccessAuditWriter
    {
        public Task WriteAdministratorCrossBranchAccessAsync(ApexPost.Application.Security.ActorContext actor, Guid branchId, string operation, CancellationToken cancellationToken) => Task.FromException(new IOException("storage failure"));
        public Task WriteOperationAsync(ApexPost.Application.Security.ActorContext? actor, Guid? branchId, string operation, string targetType, string targetReference, string outcome, IReadOnlyDictionary<string, string>? safeMetadata, CancellationToken cancellationToken) => Task.FromException(new IOException("storage failure"));
    }
    private static BookShipmentRequest ShipmentRequest(string number) => new BookShipmentRequest(Guid.NewGuid(), number, "Asha Rao", "1 Market Road", "Bengaluru", "560001", "IN", "Rahul Verma", "2 Lake Road", "Mysuru", "570001", "IN", Guid.NewGuid(), 1m, "Documents", 10m, "INR") with { ZoneCode = "Z01", ServiceLevel = ShippingServiceLevel.Standard };
    private static ShipmentOperationsUseCase NewShipmentUseCase(IShipmentRepository repository, decimal maximumWeight = 70m) => new(repository, new PriceLookup(), new ShippingRateLookup(), OperationalAccessFixture.Create(), new ShipmentPolicy(maximumWeight));
    private static IssueMoneyOrderRequest MoneyOrderRequest(string number) => new(number, 100m, "INR", Guid.NewGuid(), "Asha Rao", "BEN-1");
    private static MoneyOrderOperationsUseCase NewMoneyOrderUseCase(IMoneyOrderRepository orders, VerificationRepository? verifications = null) =>
        new(orders, OperationalAccessFixture.Create(), verifications ?? new VerificationRepository(), new MoneyOrderPolicy(500m));

    [Fact]
    public async Task HighValueMoneyOrderRequiresVerifiedIdentityFromSameBranch()
    {
        var request = MoneyOrderRequest("MO-900001") with { Amount = 500.01m };
        var repository = new VerificationRepository();
        var useCase = NewMoneyOrderUseCase(new MoneyOrderRepositoryDouble(), repository);
        var missing = await useCase.IssueAsync(request, default);
        var verificationId = Guid.NewGuid();
        request = request with { IdentityVerificationId = verificationId };
        var wrongBranch = new IdentityVerificationRecord(EntityId.From(verificationId), EntityId.New(), VerificationMethod.InPersonReview, EntityId.New());
        wrongBranch.MarkVerified(DateTimeOffset.UtcNow);
        repository.Set(wrongBranch, 1);
        var foreign = await useCase.IssueAsync(request with { OrderNumber = "MO-900002" }, default);
        var verified = new IdentityVerificationRecord(EntityId.From(verificationId), EntityId.New(), VerificationMethod.InPersonReview, EntityId.From(request.OriginatingBranchId));
        verified.MarkVerified(DateTimeOffset.UtcNow);
        repository.Set(verified, 1);
        var accepted = await useCase.IssueAsync(request with { OrderNumber = "MO-900003" }, default);

        Assert.Equal("identity-verification.required", Assert.Single(missing.Errors).Code);
        Assert.Equal("identity-verification.invalid", Assert.Single(foreign.Errors).Code);
        Assert.True(accepted.IsSuccess);
        Assert.Equal(verificationId, accepted.Value.IdentityVerificationId);
    }

    [Fact]
    public async Task MoneyOrderAtVerificationThresholdDoesNotRequireIdentityVerification()
    {
        var issued = await NewMoneyOrderUseCase(new MoneyOrderRepositoryDouble()).IssueAsync(MoneyOrderRequest("MO-900004") with { Amount = 500m }, default);
        Assert.True(issued.IsSuccess);
    }

    private sealed class VerificationRepository : IIdentityVerificationRepository
    {
        private Versioned<IdentityVerificationRecord>? record;
        public void Set(IdentityVerificationRecord value, long version) => record = new(value, version);
        public Task<IReadOnlyList<Versioned<IdentityVerificationRecord>>> ListAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<Versioned<IdentityVerificationRecord>>>(record is null ? [] : [record]);
        public Task<Versioned<IdentityVerificationRecord>?> GetByIdAsync(EntityId id, CancellationToken token) => Task.FromResult(record?.Value.Id == id ? record : null);
        public Task<bool> TryAddAsync(IdentityVerificationRecord value, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(IdentityVerificationRecord value, long version, CancellationToken token) => Task.FromResult(false);
    }

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
        public Task<IReadOnlyList<Versioned<MoneyOrder>>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Versioned<MoneyOrder>>>(items.Values.ToArray());
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

    private sealed class ShippingRateLookup : IShippingRateLookup
    {
        public Task<decimal?> GetCurrentRateAsync(string zoneCode, ShippingServiceLevel serviceLevel, string currencyCode, CancellationToken cancellationToken) =>
            Task.FromResult<decimal?>(zoneCode == "Z01" && serviceLevel == ShippingServiceLevel.Standard && currencyCode == "INR" ? 10m : null);
    }
}
