using ApexPost.Application.Reporting;

namespace ApexPost.Web.Models;

public sealed record DashboardViewModel(
    string DisplayName,
    string Role,
    string BranchName,
    string EnvironmentName,
    bool DemonstrationDataEnabled,
    int? InventoryItemCount,
    int? LowStockCount,
    int? ShipmentCount,
    int? PendingShipmentCount,
    int? TransactionCount,
    int? OpenTransactionCount,
    OperationalSummaryReport? Reports);
