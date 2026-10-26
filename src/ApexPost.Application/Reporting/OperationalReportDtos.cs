namespace ApexPost.Application.Reporting;

public sealed record TransactionReport(int TotalCount, int IssuedCount, int PaidCount, decimal TotalValue, decimal AverageValue);
public sealed record PaymentReport(int TotalCount, int PendingCount, int PaidCount, decimal TotalCollected);
public sealed record ShipmentReport(int BookedCount, int AcceptedCount, int InTransitCount, int DeliveredCount, int CancelledCount, decimal TotalCharge);
public sealed record InventoryReport(int ItemCount, int UnitsOnHand, int ReorderAlerts, int LowStockCount);
public sealed record BranchActivityEntry(Guid BranchId, string BranchCode, string BranchName, int InventoryItems, int ReorderAlerts, int MoneyOrderCount, int LowStockCount = 0, int PendingIdentityVerifications = 0, int VerifiedIdentityVerifications = 0, int RejectedIdentityVerifications = 0);
public sealed record BranchActivityReport(IReadOnlyList<BranchActivityEntry> Branches);
public sealed record DailyBranchSalesEntry(Guid BranchId, string BranchCode, string BranchName, DateOnly BusinessDate, int TransactionCount, decimal RetailSales, decimal ServiceSales);
public sealed record DailyBranchSalesReport(IReadOnlyList<DailyBranchSalesEntry> Entries);
public sealed record ShipmentVolumeEntry(string ZoneCode, ApexPost.Domain.Shipping.ShippingServiceLevel ServiceLevel, int ShipmentCount);
public sealed record OperationalSummaryReport(TransactionReport Transactions, PaymentReport Payments, ShipmentReport Shipments, InventoryReport Inventory, BranchActivityReport BranchActivity, DateTimeOffset GeneratedAtUtc)
{
    public DailyBranchSalesReport DailySales { get; init; } = new([]);
    public IReadOnlyList<ShipmentVolumeEntry> ShipmentVolumeByZoneAndServiceLevel { get; init; } = [];
}
