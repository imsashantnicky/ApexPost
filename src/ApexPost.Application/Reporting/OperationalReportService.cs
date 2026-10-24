using ApexPost.Application.Branches;
using ApexPost.Application.Inventory;
using ApexPost.Application.MoneyOrders;
using ApexPost.Application.Persistence;
using ApexPost.Application.Shipping;
using ApexPost.Application.Transactions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.MoneyOrders;
using ApexPost.Domain.Payments;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Reporting;

public sealed class OperationalReportService
{
    private readonly IBranchRepository branchRepository;
    private readonly IInventoryItemRepository inventoryRepository;
    private readonly IMoneyOrderRepository moneyOrderRepository;
    private readonly IShipmentRepository shipmentRepository;
    private readonly IRetailTransactionRepository transactionRepository;

    public OperationalReportService(
        IBranchRepository branchRepository,
        IInventoryItemRepository inventoryRepository,
        IMoneyOrderRepository moneyOrderRepository,
        IShipmentRepository shipmentRepository,
        IRetailTransactionRepository transactionRepository)
    {
        this.branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        this.inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
        this.moneyOrderRepository = moneyOrderRepository ?? throw new ArgumentNullException(nameof(moneyOrderRepository));
        this.shipmentRepository = shipmentRepository ?? throw new ArgumentNullException(nameof(shipmentRepository));
        this.transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
    }

    public async Task<OperationalSummaryReport> BuildAsync(CancellationToken cancellationToken)
    {
        var branches = await branchRepository.ListAsync(cancellationToken);
        var inventory = await inventoryRepository.ListAsync(cancellationToken);
        var moneyOrders = await moneyOrderRepository.ListAsync(cancellationToken);
        var shipments = await shipmentRepository.ListAsync(cancellationToken);
        var transactions = await transactionRepository.ListAsync(cancellationToken);

        var transactionReport = BuildTransactionReport(transactions);
        var paymentReport = BuildPaymentReport(transactions);
        var shipmentReport = BuildShipmentReport(shipments);
        var inventoryReport = BuildInventoryReport(inventory);
        var branchActivity = BuildBranchActivity(branches, inventory, moneyOrders);

        return new OperationalSummaryReport(transactionReport, paymentReport, shipmentReport, inventoryReport, branchActivity, DateTimeOffset.UtcNow);
    }

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
        IReadOnlyList<Versioned<MoneyOrder>> moneyOrders)
    {
        var groupedInventory = inventory.GroupBy(x => x.Value.BranchId.Value).ToDictionary(x => x.Key, x => x.Count());
        var reorderAlerts = inventory.GroupBy(x => x.Value.BranchId.Value).ToDictionary(x => x.Key, x => x.Count(y => y.Value.NeedsReorder));
        var moneyOrderCounts = moneyOrders.GroupBy(x => x.Value.OriginatingBranchId.Value).ToDictionary(x => x.Key, x => x.Count());

        var entries = branches.Select(branch => new BranchActivityEntry(
            branch.Value.Id.Value,
            branch.Value.Code.Value,
            branch.Value.Name,
            groupedInventory.TryGetValue(branch.Value.Id.Value, out var inventoryCount) ? inventoryCount : 0,
            reorderAlerts.TryGetValue(branch.Value.Id.Value, out var alertCount) ? alertCount : 0,
            moneyOrderCounts.TryGetValue(branch.Value.Id.Value, out var orderCount) ? orderCount : 0)).ToArray();

        return new BranchActivityReport(entries);
    }
}
