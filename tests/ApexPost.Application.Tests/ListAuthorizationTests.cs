using ApexPost.Application.IdentityVerification;
using ApexPost.Application.Inventory;
using ApexPost.Application.MoneyOrders;
using ApexPost.Application.Persistence;
using ApexPost.Application.Shipping;
using ApexPost.Application.Transactions;
using ApexPost.Domain.Common;
using ApexPost.Domain.IdentityVerification;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.MoneyOrders;
using ApexPost.Domain.Pricing;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Staff;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Tests;

/// <summary>
/// List pages authorize the actor once and filter rows by branch scope. The trainer saw 784 per-row
/// "Inventory.List" denials; a list call must now write at most one audit entry.
/// </summary>
public sealed class ListAuthorizationTests
{
    private static readonly Guid OwnBranch = Guid.NewGuid();
    private static readonly Guid[] OtherBranches = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
    private static readonly Guid[] AllBranches = [OwnBranch, .. OtherBranches];

    public static TheoryData<string> Lists => ["Inventory", "Transactions", "Shipments", "MoneyOrders"];

    [Theory]
    [MemberData(nameof(Lists))]
    public async Task List_AsAdministrator_ReturnsEveryBranchAndWritesNoAuditEntry(string list)
    {
        var audit = new InMemoryAuditStore();

        var (succeeded, branchIds) = await ListAsync(list, "Administrator", EmploymentStatus.Active, audit);

        Assert.True(succeeded);
        Assert.Equal(AllBranches.Order(), branchIds.Distinct().Order());
        Assert.Empty(audit.Entries);
    }

    [Theory]
    [MemberData(nameof(Lists))]
    public async Task List_AsBranchManager_ReturnsOnlyOwnBranchAndWritesNoAuditEntry(string list)
    {
        var audit = new InMemoryAuditStore();

        var (succeeded, branchIds) = await ListAsync(list, "BranchManager", EmploymentStatus.Active, audit);

        Assert.True(succeeded);
        Assert.NotEmpty(branchIds);
        Assert.All(branchIds, branchId => Assert.Equal(OwnBranch, branchId));
        Assert.Empty(audit.Entries);
    }

    [Theory]
    [MemberData(nameof(Lists))]
    public async Task List_WhenActorIsDenied_WritesExactlyOneAuditEntry(string list)
    {
        var audit = new InMemoryAuditStore();

        var (succeeded, _) = await ListAsync(list, "BranchManager", EmploymentStatus.Suspended, audit);

        Assert.False(succeeded);
        var entry = Assert.Single(audit.Entries);
        Assert.Equal("Denied", entry.Outcome);
    }

    private static async Task<(bool Succeeded, Guid[] BranchIds)> ListAsync(string list, string role, EmploymentStatus status, InMemoryAuditStore audit)
    {
        var access = OperationalAccessFixture.Create(branchId: OwnBranch, role: role, status: status, auditStore: audit);
        switch (list)
        {
            case "Inventory":
            {
                var result = await new InventoryOperationsUseCase(new Inventory(), access).ListAsync(default);
                return (result.IsSuccess, result.IsSuccess ? result.Value.Select(item => item.BranchId).ToArray() : []);
            }
            case "Transactions":
            {
                var transactions = new Transactions();
                var result = await new TransactionOperationsUseCase(transactions, new NoPrices(), transactions, access, TestClock.Create()).ListAsync(default);
                return (result.IsSuccess, result.IsSuccess ? result.Value.Select(item => item.BranchId).ToArray() : []);
            }
            case "Shipments":
            {
                var result = await new ShipmentOperationsUseCase(new Shipments(), new NoPrices(), new NoRates(), access, new ShipmentPolicy(70m)).ListAsync(default);
                return (result.IsSuccess, result.IsSuccess ? result.Value.Select(item => item.BranchId).ToArray() : []);
            }
            default:
            {
                var result = await new MoneyOrderOperationsUseCase(new MoneyOrders(), access, new NoVerifications(), new MoneyOrderPolicy(500m)).ListAsync(default);
                return (result.IsSuccess, result.IsSuccess ? result.Value.Select(item => item.OriginatingBranchId).ToArray() : []);
            }
        }
    }

    // Several rows per branch, so a per-row check would have produced many audit entries.
    private static IEnumerable<(Guid BranchId, int Index)> Rows() =>
        AllBranches.SelectMany(branchId => Enumerable.Range(1, 5).Select(index => (branchId, index)));

    private abstract class ListOnly<T>(IEnumerable<T> items) where T : class
    {
        private readonly Versioned<T>[] rows = items.Select(item => new Versioned<T>(item, 1)).ToArray();
        public Task<IReadOnlyList<Versioned<T>>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Versioned<T>>>(rows);
        public Task<Versioned<T>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> TryAddAsync(T item, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> TryUpdateAsync(T item, long expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Inventory() : ListOnly<InventoryItem>(Rows().Select(row =>
        new InventoryItem(EntityId.New(), EntityId.From(row.BranchId), InventorySku.From($"SKU-{row.Index:000}"), "Item", 1))), IInventoryItemRepository;

    private sealed class Transactions() : ListOnly<RetailTransaction>(Rows().Select(row =>
        new RetailTransaction(EntityId.New(), EntityId.From(row.BranchId), $"C-{row.Index}", "INR"))), IRetailTransactionRepository, ITransactionPaymentCoordinator
    {
        public Task<Versioned<RetailTransaction>?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PaymentStartWriteResult> TryStartPaymentAsync(RetailTransaction transaction, long expectedVersion, string idempotencyKey, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Shipments() : ListOnly<Shipment>(Rows().Select(row =>
        Shipment.Restore(EntityId.New(), ShipmentNumber.From($"HS-{row.Index:000000}"),
            new ShipmentParty("Sender", new ShipmentAddress("1 Road", "Pune", "411001", "IN")),
            new ShipmentParty("Recipient", new ShipmentAddress("2 Road", "Mumbai", "400001", "IN")),
            EntityId.New(), new ShipmentPackage(1m, "Goods"), new ShipmentCharge(80m, "INR"), ShipmentStatus.Booked,
            EntityId.From(row.BranchId), "Z01", ShippingServiceLevel.Standard))), IShipmentRepository;

    private sealed class MoneyOrders() : ListOnly<MoneyOrder>(Rows().Select(row =>
        new MoneyOrder(EntityId.New(), MoneyOrderNumber.From($"MO-{row.Index:000000}"), new MoneyOrderAmount(100m, "INR"),
            EntityId.From(row.BranchId), new MoneyOrderBeneficiary("Beneficiary", "REF-1")))), IMoneyOrderRepository;

    private sealed class NoVerifications() : ListOnly<IdentityVerificationRecord>([]), IIdentityVerificationRepository;

    private sealed class NoPrices : IServicePriceLookup
    {
        public Task<ServicePrice?> GetCurrentPriceAsync(EntityId serviceId, string currencyCode, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoRates : IShippingRateLookup
    {
        public Task<decimal?> GetCurrentRateAsync(string zoneCode, ShippingServiceLevel serviceLevel, string currencyCode, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
