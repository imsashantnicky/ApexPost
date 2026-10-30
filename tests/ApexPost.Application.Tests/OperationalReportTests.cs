using ApexPost.Application.Branches;
using ApexPost.Application.IdentityVerification;
using ApexPost.Application.Inventory;
using ApexPost.Application.MoneyOrders;
using ApexPost.Application.Persistence;
using ApexPost.Application.Reporting;
using ApexPost.Application.Security;
using ApexPost.Application.Shipping;
using ApexPost.Application.Staff;
using ApexPost.Application.Transactions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.IdentityVerification;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.MoneyOrders;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Staff;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Tests;

public sealed class OperationalReportTests
{
    [Fact]
    public async Task DailySalesUsesUtcBusinessDatesSplitsKindsAndExcludesUnpaidOrReversedTransactions()
    {
        var fixture = new Fixture();
        var branch = fixture.BranchId;
        var transactions = new RetailTransaction[]
        {
            Paid(branch, "A", new DateTimeOffset(2026, 10, 26, 23, 30, 0, TimeSpan.FromHours(-4)), retail: 20m, services: 30m),
            Paid(branch, "B", new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero), retail: 5m, services: 10m),
            Paid(branch, "C", new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero), retail: 7m, services: 8m),
            Reversed(branch, "D", new DateTimeOffset(2026, 10, 27, 13, 0, 0, TimeSpan.Zero)),
            new RetailTransaction(EntityId.New(), EntityId.From(branch), "UNPAID", "INR", new DateTimeOffset(2026, 10, 27, 14, 0, 0, TimeSpan.Zero))
        };
        fixture.Transactions.Items = transactions.Select((item, index) => new Versioned<RetailTransaction>(item, index + 1)).ToArray();

        var result = await fixture.Service.BuildAsync(default);

        Assert.True(result.IsSuccess);
        var today = Assert.Single(result.Value.DailySales.Entries, entry => entry.BusinessDate == new DateOnly(2026, 10, 27));
        Assert.Equal(2, today.TransactionCount);
        Assert.Equal(25m, today.RetailSales);
        Assert.Equal(40m, today.ServiceSales);
        var previous = Assert.Single(result.Value.DailySales.Entries, entry => entry.BusinessDate == new DateOnly(2026, 10, 26));
        Assert.Equal(7m, previous.RetailSales);
        Assert.Equal(8m, previous.ServiceSales);
    }

    [Fact]
    public async Task ReportsIncludeLowStockMoneyOrderVerificationAndZoneLevelGroupsWithinManagerBranch()
    {
        var fixture = new Fixture();
        var item = new InventoryItem(EntityId.New(), EntityId.From(fixture.BranchId), InventorySku.From("ENV-901"), "Synthetic low stock", 3);
        item.Receive(2, "Synthetic opening stock", DateTimeOffset.UtcNow);
        fixture.Inventory.Items = [new(item, 1)];
        fixture.Shipments.Items = [new(CreateShipment(fixture.BranchId, "Z01", ShippingServiceLevel.Express), 1), new(CreateShipment(Guid.NewGuid(), "Z02", ShippingServiceLevel.Standard), 1)];
        fixture.MoneyOrders.Items = [new(CreateMoneyOrder(fixture.BranchId, "MO-901001"), 1), new(CreateMoneyOrder(Guid.NewGuid(), "MO-901002"), 1)];
        fixture.Verifications.Items = [new(CreateVerification(fixture.BranchId, VerificationStatus.Pending), 1), new(CreateVerification(fixture.BranchId, VerificationStatus.Verified), 1), new(CreateVerification(Guid.NewGuid(), VerificationStatus.Rejected), 1)];

        var result = await fixture.Service.BuildAsync(default);

        Assert.True(result.IsSuccess);
        var branch = Assert.Single(result.Value.BranchActivity.Branches);
        Assert.Equal(fixture.BranchId, branch.BranchId);
        Assert.Equal(1, branch.LowStockCount);
        Assert.Equal(1, branch.MoneyOrderCount);
        Assert.Equal(1, branch.PendingIdentityVerifications);
        Assert.Equal(1, branch.VerifiedIdentityVerifications);
        Assert.Equal(0, branch.RejectedIdentityVerifications);
        var volume = Assert.Single(result.Value.ShipmentVolumeByZoneAndServiceLevel);
        Assert.Equal("Z01", volume.ZoneCode);
        Assert.Equal(ShippingServiceLevel.Express, volume.ServiceLevel);
        Assert.Equal(1, volume.ShipmentCount);
    }

    [Fact]
    public async Task EmptyOperationalCollectionsProduceEmptySalesAndShippingReports()
    {
        var result = await new Fixture().Service.BuildAsync(default);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.DailySales.Entries);
        Assert.Empty(result.Value.ShipmentVolumeByZoneAndServiceLevel);
        Assert.Equal(0, result.Value.Inventory.LowStockCount);
        var branch = Assert.Single(result.Value.BranchActivity.Branches);
        Assert.Equal(0, branch.MoneyOrderCount);
    }

    private static RetailTransaction Paid(Guid branch, string reference, DateTimeOffset timestamp, decimal retail, decimal services)
    {
        var transaction = new RetailTransaction(EntityId.New(), EntityId.From(branch), reference, "INR", timestamp);
        transaction.AddLineItem(EntityId.New(), "Sample retail item", 1, retail, TransactionLineKind.RetailProduct);
        transaction.AddLineItem(EntityId.New(), "Sample counter service", 1, services, TransactionLineKind.Service);
        transaction.StartPayment(transaction.Total, ApexPost.Domain.Payments.PaymentMethod.Cash, $"TEST-{reference}");
        transaction.ConfirmPayment();
        return transaction;
    }

    private static RetailTransaction Reversed(Guid branch, string reference, DateTimeOffset timestamp)
    {
        var transaction = Paid(branch, reference, timestamp, 100m, 100m);
        transaction.ReversePayment("Synthetic test reversal");
        return transaction;
    }

    private static Shipment CreateShipment(Guid branch, string zone, ShippingServiceLevel level) => new(
        EntityId.New(), ShipmentNumber.From("TS-000001"),
        new ShipmentParty("Sample sender", new ShipmentAddress("1 Sample Road", "Pune", "411001", "IN")),
        new ShipmentParty("Sample recipient", new ShipmentAddress("2 Sample Road", "Mumbai", "400001", "IN")),
        EntityId.New(), new ShipmentPackage(1m, "Synthetic goods"), new ShipmentCharge(100m, "INR"), EntityId.From(branch), zone, level);

    private static MoneyOrder CreateMoneyOrder(Guid branch, string number) => new(
        EntityId.New(), MoneyOrderNumber.From(number), new MoneyOrderAmount(100m, "INR"), EntityId.From(branch), new MoneyOrderBeneficiary("Sample person", "SAMPLE-REF"));

    private static IdentityVerificationRecord CreateVerification(Guid branch, VerificationStatus status)
    {
        var record = new IdentityVerificationRecord(EntityId.New(), EntityId.New(), VerificationMethod.InPersonReview, EntityId.From(branch));
        if (status == VerificationStatus.Verified) record.MarkVerified(DateTimeOffset.UtcNow);
        else if (status == VerificationStatus.Rejected) record.Reject("Synthetic rejection", DateTimeOffset.UtcNow);
        return record;
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            BranchId = Guid.NewGuid();
            StaffId = Guid.NewGuid();
            var branch = new Branch(EntityId.From(BranchId), BranchCode.From("TST-777"), "Report Test Branch", new BranchAddress("1 Test Road", null, "Test City", "12345", "IN"), 1);
            Branches = new BranchRepository(branch);
            var staff = new StaffMember(EntityId.From(StaffId), EmployeeNumber.From("EMP-7777"), "Report Test Manager", EntityId.From(BranchId), [StaffRole.BranchManager]);
            CurrentActor = new CurrentActor(new ActorContext(StaffId, "BranchManager", BranchId, "report-test"));
            Staff = new StaffRepository(staff);
            Access = new OperationalAccess(CurrentActor, Staff, Branches, new InMemoryAuditStore().Trail());
            Inventory = new InventoryRepository();
            Shipments = new ShipmentRepository();
            Transactions = new TransactionRepository();
            MoneyOrders = new MoneyOrderRepository();
            Verifications = new VerificationRepository();
            Service = new OperationalReportService(Branches, Inventory, MoneyOrders, Shipments, Transactions, Verifications, CurrentActor, Staff, Access);
        }

        public Guid BranchId { get; }
        public Guid StaffId { get; }
        public BranchRepository Branches { get; }
        public CurrentActor CurrentActor { get; }
        public StaffRepository Staff { get; }
        public OperationalAccess Access { get; }
        public InventoryRepository Inventory { get; }
        public ShipmentRepository Shipments { get; }
        public TransactionRepository Transactions { get; }
        public MoneyOrderRepository MoneyOrders { get; }
        public VerificationRepository Verifications { get; }
        public OperationalReportService Service { get; }
    }

    private sealed class CurrentActor(ActorContext? actor) : ICurrentActor { public ActorContext? Actor => actor; }

    private sealed class BranchRepository(Branch branch) : IBranchRepository
    {
        public Task<IReadOnlyList<Versioned<Branch>>> ListAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<Versioned<Branch>>>([new(branch, 1)]);
        public Task<Versioned<Branch>?> GetByIdAsync(EntityId id, CancellationToken token) => Task.FromResult<Versioned<Branch>?>(id == branch.Id ? new(branch, 1) : null);
        public Task<bool> TryAddAsync(Branch value, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(Branch value, long version, CancellationToken token) => Task.FromResult(false);
    }

    private sealed class StaffRepository(StaffMember staff) : IStaffRepository
    {
        public Task<IReadOnlyList<Versioned<StaffMember>>> ListAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<Versioned<StaffMember>>>([new(staff, 1)]);
        public Task<Versioned<StaffMember>?> GetByIdAsync(EntityId id, CancellationToken token) => Task.FromResult<Versioned<StaffMember>?>(id == staff.Id ? new(staff, 1) : null);
        public Task<bool> TryAddAsync(StaffMember value, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(StaffMember value, long version, CancellationToken token) => Task.FromResult(false);
    }

    private sealed class InventoryRepository : IInventoryItemRepository
    {
        public IReadOnlyList<Versioned<InventoryItem>> Items { get; set; } = [];
        public Task<IReadOnlyList<Versioned<InventoryItem>>> ListAsync(CancellationToken token) => Task.FromResult(Items);
        public Task<Versioned<InventoryItem>?> GetByIdAsync(EntityId id, CancellationToken token) => Task.FromResult<Versioned<InventoryItem>?>(Items.SingleOrDefault(item => item.Value.Id == id));
        public Task<bool> TryAddAsync(InventoryItem item, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(InventoryItem item, long version, CancellationToken token) => Task.FromResult(false);
    }

    private sealed class ShipmentRepository : IShipmentRepository
    {
        public IReadOnlyList<Versioned<Shipment>> Items { get; set; } = [];
        public Task<IReadOnlyList<Versioned<Shipment>>> ListAsync(CancellationToken token) => Task.FromResult(Items);
        public Task<Versioned<Shipment>?> GetByIdAsync(EntityId id, CancellationToken token) => Task.FromResult<Versioned<Shipment>?>(Items.SingleOrDefault(item => item.Value.Id == id));
        public Task<bool> TryAddAsync(Shipment item, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(Shipment item, long version, CancellationToken token) => Task.FromResult(false);
    }

    private sealed class TransactionRepository : IRetailTransactionRepository, ITransactionPaymentCoordinator
    {
        public IReadOnlyList<Versioned<RetailTransaction>> Items { get; set; } = [];
        public Task<IReadOnlyList<Versioned<RetailTransaction>>> ListAsync(CancellationToken token) => Task.FromResult(Items);
        public Task<Versioned<RetailTransaction>?> GetByIdAsync(EntityId id, CancellationToken token) => Task.FromResult<Versioned<RetailTransaction>?>(Items.SingleOrDefault(item => item.Value.Id == id));
        public Task<bool> TryAddAsync(RetailTransaction item, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(RetailTransaction item, long version, CancellationToken token) => Task.FromResult(false);
        public Task<Versioned<RetailTransaction>?> GetByIdempotencyKeyAsync(string key, CancellationToken token) => Task.FromResult<Versioned<RetailTransaction>?>(null);
        public Task<PaymentStartWriteResult> TryStartPaymentAsync(RetailTransaction transaction, long version, string key, CancellationToken token) => Task.FromResult(new PaymentStartWriteResult(null, false));
    }

    private sealed class MoneyOrderRepository : IMoneyOrderRepository
    {
        public IReadOnlyList<Versioned<MoneyOrder>> Items { get; set; } = [];
        public Task<IReadOnlyList<Versioned<MoneyOrder>>> ListAsync(CancellationToken token) => Task.FromResult(Items);
        public Task<Versioned<MoneyOrder>?> GetByIdAsync(EntityId id, CancellationToken token) => Task.FromResult<Versioned<MoneyOrder>?>(Items.SingleOrDefault(item => item.Value.Id == id));
        public Task<bool> TryAddAsync(MoneyOrder item, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(MoneyOrder item, long version, CancellationToken token) => Task.FromResult(false);
    }

    private sealed class VerificationRepository : IIdentityVerificationRepository
    {
        public IReadOnlyList<Versioned<IdentityVerificationRecord>> Items { get; set; } = [];
        public Task<IReadOnlyList<Versioned<IdentityVerificationRecord>>> ListAsync(CancellationToken token) => Task.FromResult(Items);
        public Task<Versioned<IdentityVerificationRecord>?> GetByIdAsync(EntityId id, CancellationToken token) => Task.FromResult<Versioned<IdentityVerificationRecord>?>(Items.SingleOrDefault(item => item.Value.Id == id));
        public Task<bool> TryAddAsync(IdentityVerificationRecord item, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(IdentityVerificationRecord item, long version, CancellationToken token) => Task.FromResult(false);
    }
}
