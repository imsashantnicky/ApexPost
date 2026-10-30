using ApexPost.Application.Branches;
using ApexPost.Application.Inventory;
using ApexPost.Application.MoneyOrders;
using ApexPost.Application.Persistence;
using ApexPost.Application.Shipping;
using ApexPost.Application.Transactions;
using ApexPost.Application.IdentityVerification;
using ApexPost.Application.Results;
using ApexPost.Application.Security;
using ApexPost.Application.Staff;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.MoneyOrders;
using ApexPost.Domain.Payments;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;
using ApexPost.Domain.IdentityVerification;
using ApexPost.Domain.Staff;

namespace ApexPost.Application.Reporting;

public sealed class OperationalReportService
{
    private readonly IBranchRepository branchRepository;
    private readonly IInventoryItemRepository inventoryRepository;
    private readonly IMoneyOrderRepository moneyOrderRepository;
    private readonly IShipmentRepository shipmentRepository;
    private readonly IRetailTransactionRepository transactionRepository;
    private readonly IIdentityVerificationRepository verificationRepository;
    private readonly ICurrentActor currentActor;
    private readonly IStaffRepository staffRepository;
    private readonly OperationalAccess access;
    private readonly TimeProvider timeProvider;

    public OperationalReportService(
        IBranchRepository branchRepository,
        IInventoryItemRepository inventoryRepository,
        IMoneyOrderRepository moneyOrderRepository,
        IShipmentRepository shipmentRepository,
        IRetailTransactionRepository transactionRepository,
        IIdentityVerificationRepository verificationRepository,
        ICurrentActor currentActor,
        IStaffRepository staffRepository,
        OperationalAccess access,
        TimeProvider timeProvider)
    {
        this.branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        this.inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        this.moneyOrderRepository = moneyOrderRepository ?? throw new ArgumentNullException(nameof(moneyOrderRepository));
        this.shipmentRepository = shipmentRepository ?? throw new ArgumentNullException(nameof(shipmentRepository));
        this.transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        this.verificationRepository = verificationRepository ?? throw new ArgumentNullException(nameof(verificationRepository));
        this.currentActor = currentActor ?? throw new ArgumentNullException(nameof(currentActor));
        this.staffRepository = staffRepository ?? throw new ArgumentNullException(nameof(staffRepository));
        this.access = access ?? throw new ArgumentNullException(nameof(access));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ApplicationResult<OperationalSummaryReport>> BuildAsync(CancellationToken cancellationToken)
    {
        var actor = currentActor.Actor;
        if (actor is null || actor.StaffId == Guid.Empty || actor.AssignedBranchId is not Guid assignedBranch || assignedBranch == Guid.Empty)
            return Failure("actor.required", "An authenticated staff actor is required.");
        var staff = await staffRepository.GetByIdAsync(EntityId.From(actor.StaffId), cancellationToken);
        if (staff is null) return Failure("actor.not-found", "The authenticated staff actor is unavailable.");
        if (staff.Value.Status != EmploymentStatus.Active || staff.Value.BranchId.Value != assignedBranch)
            return Failure("actor.inactive", "The authenticated staff actor is unavailable.");

        if (!StaffRoleMapping.MatchesStaff(actor.Role, staff.Value.Roles) || !StaffRoleMapping.CanReadReports(actor.Role))
            return Failure("access.denied", "The actor is not authorized to view operational reports.");

        var branchScoped = StaffRoleMapping.Normalize(actor.Role) == Roles.BranchManager;
        var branches = await branchRepository.ListAsync(cancellationToken);
        if (branchScoped)
        {
            var authorized = await access.CheckAsync(assignedBranch, OperationalCapability.Inventory, "OperationalReport.Build", cancellationToken, requireOpenBranch: false);
            if (!authorized.IsSuccess) return ApplicationResults.Failure<OperationalSummaryReport>(authorized.Errors);
            branches = branches.Where(x => x.Value.Id.Value == assignedBranch).ToArray();
        }
        // Administrators and auditors were validated above and read every branch; no per-branch check or audit row.
        var inventory = await inventoryRepository.ListAsync(cancellationToken);
        var moneyOrders = await moneyOrderRepository.ListAsync(cancellationToken);
        var shipments = await shipmentRepository.ListAsync(cancellationToken);
        var transactions = await transactionRepository.ListAsync(cancellationToken);
        var verifications = await verificationRepository.ListAsync(cancellationToken);

        if (branchScoped)
        {
            inventory = inventory.Where(x => x.Value.BranchId.Value == assignedBranch).ToArray();
            moneyOrders = moneyOrders.Where(x => x.Value.OriginatingBranchId.Value == assignedBranch).ToArray();
            shipments = shipments.Where(x => x.Value.BranchId?.Value == assignedBranch).ToArray();
            transactions = transactions.Where(x => x.Value.BranchId.Value == assignedBranch).ToArray();
            verifications = verifications.Where(x => x.Value.BranchId?.Value == assignedBranch).ToArray();
        }

        var transactionReport = BuildTransactionReport(transactions);
        var paymentReport = BuildPaymentReport(transactions);
        var shipmentReport = BuildShipmentReport(shipments);
        var inventoryReport = BuildInventoryReport(inventory);
        var branchActivity = BuildBranchActivity(branches, inventory, moneyOrders, verifications);
        var dailySales = BuildDailyBranchSales(branches, transactions);
        var shipmentVolume = BuildShipmentVolume(shipments);

        return ApplicationResults.Success(new OperationalSummaryReport(transactionReport, paymentReport, shipmentReport, inventoryReport, branchActivity, timeProvider.GetUtcNow())
        {
            DailySales = dailySales,
            ShipmentVolumeByZoneAndServiceLevel = shipmentVolume
        });
    }

    private static ApplicationResult<OperationalSummaryReport> Failure(string code, string message) =>
        ApplicationResults.Failure<OperationalSummaryReport>([new(code, message)]);

    private static TransactionReport BuildTransactionReport(IReadOnlyList<Versioned<RetailTransaction>> transactions)
    {
        var totalCount = transactions.Count;
        var paidCount = transactions.Count(x => x.Value.Status == TransactionStatus.Paid || x.Value.Status == TransactionStatus.Reversed);
        var issuedCount = transactions.Count(x => x.Value.Status == TransactionStatus.Open || x.Value.Status == TransactionStatus.PaymentPending || x.Value.Status == TransactionStatus.Cancelled);
        var totalValue = transactions.Sum(x => x.Value.Total);
        var averageValue = totalCount == 0 ? 0m : totalValue / totalCount;
        return new TransactionReport(totalCount, issuedCount, paidCount, totalValue, averageValue);
    }

    private static PaymentReport BuildPaymentReport(IReadOnlyList<Versioned<RetailTransaction>> transactions)
    {
        var allPayments = transactions.SelectMany(static x => x.Value.Payments).ToArray();
        var totalCount = allPayments.Length;
        var pendingCount = allPayments.Count(static payment => payment.Status == PaymentStatus.Pending);
        var paidCount = allPayments.Count(static payment => payment.Status == PaymentStatus.Confirmed || payment.Status == PaymentStatus.Reversed);
        var totalCollected = allPayments
            .Where(static payment => payment.Status == PaymentStatus.Confirmed || payment.Status == PaymentStatus.Reversed)
            .Sum(static payment => payment.Amount);
        return new PaymentReport(totalCount, pendingCount, paidCount, totalCollected);
    }

    private static ShipmentReport BuildShipmentReport(IReadOnlyList<Versioned<Shipment>> shipments)
    {
        var bookedCount = shipments.Count(x => x.Value.Status == ShipmentStatus.Booked);
        var acceptedCount = shipments.Count(x => x.Value.Status == ShipmentStatus.Accepted);
        var inTransitCount = shipments.Count(x => x.Value.Status == ShipmentStatus.InTransit);
        var deliveredCount = shipments.Count(x => x.Value.Status == ShipmentStatus.Delivered);
        var cancelledCount = shipments.Count(x => x.Value.Status == ShipmentStatus.Cancelled);
        var totalCharge = shipments.Sum(x => x.Value.Charge.Amount);
        return new ShipmentReport(bookedCount, acceptedCount, inTransitCount, deliveredCount, cancelledCount, totalCharge);
    }

    private static DailyBranchSalesReport BuildDailyBranchSales(
        IReadOnlyList<Versioned<Branch>> branches,
        IReadOnlyList<Versioned<RetailTransaction>> transactions)
    {
        var branchById = branches.ToDictionary(branch => branch.Value.Id.Value, branch => branch.Value);
        var entries = transactions
            .Where(item => item.Value.Status == TransactionStatus.Paid && item.Value.CreatedAtUtc is not null)
            .Where(item => branchById.ContainsKey(item.Value.BranchId.Value))
            .GroupBy(item => (BranchId: item.Value.BranchId.Value, BusinessDate: DateOnly.FromDateTime(item.Value.CreatedAtUtc!.Value.UtcDateTime)))
            .Select(group =>
            {
                var branch = branchById[group.Key.BranchId];
                var retail = group.SelectMany(item => item.Value.LineItems)
                    .Where(line => line.Kind == TransactionLineKind.RetailProduct).Sum(line => line.Total);
                var services = group.SelectMany(item => item.Value.LineItems)
                    .Where(line => line.Kind == TransactionLineKind.Service).Sum(line => line.Total);
                return new DailyBranchSalesEntry(branch.Id.Value, branch.Code.Value, branch.Name, group.Key.BusinessDate, group.Count(), retail, services);
            })
            .OrderByDescending(entry => entry.BusinessDate)
            .ThenBy(entry => entry.BranchCode, StringComparer.Ordinal)
            .ToArray();

        return new DailyBranchSalesReport(entries);
    }

    private static ShipmentVolumeEntry[] BuildShipmentVolume(IReadOnlyList<Versioned<Shipment>> shipments) =>
        shipments
            .Where(item => !string.Equals(item.Value.ZoneCode, "UNKNOWN", StringComparison.Ordinal))
            .GroupBy(item => (item.Value.ZoneCode, item.Value.ServiceLevel))
            .Select(group => new ShipmentVolumeEntry(group.Key.ZoneCode, group.Key.ServiceLevel, group.Count()))
            .OrderBy(entry => entry.ZoneCode, StringComparer.Ordinal)
            .ThenBy(entry => entry.ServiceLevel)
            .ToArray();

    private static InventoryReport BuildInventoryReport(IReadOnlyList<Versioned<InventoryItem>> items)
    {
        var itemCount = items.Count;
        var unitsOnHand = items.Sum(x => x.Value.QuantityOnHand);
        var reorderAlerts = items.Count(x => x.Value.NeedsReorder);
        var lowStockCount = items.Count(x => x.Value.QuantityOnHand <= x.Value.ReorderLevel);
        return new InventoryReport(itemCount, unitsOnHand, reorderAlerts, lowStockCount);
    }

    private static BranchActivityReport BuildBranchActivity(
        IReadOnlyList<Versioned<Branch>> branches,
        IReadOnlyList<Versioned<InventoryItem>> inventory,
        IReadOnlyList<Versioned<MoneyOrder>> moneyOrders,
        IReadOnlyList<Versioned<IdentityVerificationRecord>> verifications)
    {
        var groupedInventory = inventory.GroupBy(x => x.Value.BranchId.Value).ToDictionary(x => x.Key, x => x.Count());
        var reorderAlerts = inventory.GroupBy(x => x.Value.BranchId.Value).ToDictionary(x => x.Key, x => x.Count(y => y.Value.NeedsReorder));
        var moneyOrderCounts = moneyOrders.GroupBy(x => x.Value.OriginatingBranchId.Value).ToDictionary(x => x.Key, x => x.Count());
        var lowStockCounts = inventory.GroupBy(x => x.Value.BranchId.Value).ToDictionary(x => x.Key, x => x.Count(y => y.Value.QuantityOnHand <= y.Value.ReorderLevel));
        var verificationCounts = verifications.Where(x => x.Value.BranchId is not null).GroupBy(x => x.Value.BranchId!.Value)
            .ToDictionary(x => x.Key, x => (Pending: x.Count(y => y.Value.Status == VerificationStatus.Pending), Verified: x.Count(y => y.Value.Status == VerificationStatus.Verified), Rejected: x.Count(y => y.Value.Status == VerificationStatus.Rejected)));

        var entries = branches.Select(branch => new BranchActivityEntry(
            branch.Value.Id.Value,
            branch.Value.Code.Value,
            branch.Value.Name,
            groupedInventory.TryGetValue(branch.Value.Id.Value, out var inventoryCount) ? inventoryCount : 0,
            reorderAlerts.TryGetValue(branch.Value.Id.Value, out var alertCount) ? alertCount : 0,
            moneyOrderCounts.TryGetValue(branch.Value.Id.Value, out var orderCount) ? orderCount : 0,
            lowStockCounts.TryGetValue(branch.Value.Id.Value, out var lowCount) ? lowCount : 0,
            verificationCounts.TryGetValue(branch.Value.Id.Value, out var outcomes) ? outcomes.Pending : 0,
            verificationCounts.TryGetValue(branch.Value.Id.Value, out outcomes) ? outcomes.Verified : 0,
            verificationCounts.TryGetValue(branch.Value.Id.Value, out outcomes) ? outcomes.Rejected : 0)).ToArray();

        return new BranchActivityReport(entries);
    }
}
