using System.Security.Claims;
using ApexPost.Application.Branches;
using ApexPost.Application.IdentityVerification;
using ApexPost.Application.Inventory;
using ApexPost.Application.Results;
using ApexPost.Application.Reporting;
using ApexPost.Application.Security;
using ApexPost.Application.Shipping;
using ApexPost.Application.Staff;
using ApexPost.Application.Transactions;
using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
[Route("dashboard")]
public sealed class DashboardController(
    IWebHostEnvironment environment,
    JsonSeedOptions seedOptions,
    ICurrentActor currentActor,
    IStaffRepository staffRepository,
    IBranchRepository branchRepository,
    InventoryOperationsUseCase inventory,
    ShipmentOperationsUseCase shipments,
    TransactionOperationsUseCase transactions,
    OperationalReportService reports) : Controller
{
    [HttpGet("")]
    [HttpGet("index")]
    public Task<IActionResult> Index(CancellationToken token) => ViewDashboard(token);

    [HttpGet("admin")]
    public Task<IActionResult> Admin(CancellationToken token) => ViewDashboard(token);

    [HttpGet("branch")]
    public Task<IActionResult> Branch(CancellationToken token) => ViewDashboard(token);

    [HttpGet("counter")]
    public Task<IActionResult> Counter(CancellationToken token) => ViewDashboard(token);

    [HttpGet("inventory")]
    public Task<IActionResult> Inventory(CancellationToken token) => ViewDashboard(token);

    [HttpGet("auditor")]
    public Task<IActionResult> Auditor(CancellationToken token) => ViewDashboard(token);

    private async Task<IActionResult> ViewDashboard(CancellationToken token)
    {
        var actor = currentActor.Actor;
        var role = actor?.Role ?? "Staff";
        var displayName = User.FindFirstValue(ClaimTypes.Name) ?? "Staff member";
        var branchName = "Branch unavailable";
        if (actor is not null)
        {
            var staff = await staffRepository.GetByIdAsync(EntityId.From(actor.StaffId), token);
            if (staff is not null) displayName = staff.Value.Name;
            if (actor.AssignedBranchId is Guid branchId)
            {
                var branch = await branchRepository.GetByIdAsync(EntityId.From(branchId), token);
                if (branch is not null) branchName = branch.Value.Name;
            }
        }

        int? inventoryCount = null;
        int? lowStockCount = null;
        int? shipmentCount = null;
        int? pendingShipmentCount = null;
        int? transactionCount = null;
        int? openTransactionCount = null;
        OperationalSummaryReport? report = null;

        Task<ApplicationResult<IReadOnlyList<InventoryItemResponse>>>? inventoryTask = null;
        Task<ApplicationResult<IReadOnlyList<ApexPost.Application.Shipping.ShipmentResponse>>>? shipmentTask = null;
        Task<ApplicationResult<IReadOnlyList<ApexPost.Application.Transactions.TransactionResponse>>>? transactionTask = null;
        Task<ApplicationResult<OperationalSummaryReport>>? reportTask = null;
        var independentReads = new List<Task>(4);

        if (StaffRoleMapping.Allows(role, OperationalCapability.Inventory))
        {
            inventoryTask = inventory.ListAsync(token);
            independentReads.Add(inventoryTask);
        }
        if (StaffRoleMapping.Allows(role, OperationalCapability.Counter))
        {
            shipmentTask = shipments.ListAsync(token);
            transactionTask = transactions.ListAsync(token);
            independentReads.Add(shipmentTask);
            independentReads.Add(transactionTask);
        }
        if (StaffRoleMapping.CanReadReports(role))
        {
            reportTask = reports.BuildAsync(token);
            independentReads.Add(reportTask);
        }

        await Task.WhenAll(independentReads);
        if (inventoryTask is not null)
        {
            var result = await inventoryTask;
            if (result.IsSuccess)
            {
                inventoryCount = result.Value.Count;
                lowStockCount = result.Value.Count(item => item.NeedsReorder);
            }
        }
        if (shipmentTask is not null)
        {
            var result = await shipmentTask;
            if (result.IsSuccess)
            {
                shipmentCount = result.Value.Count;
                pendingShipmentCount = result.Value.Count(item => item.Status is ApexPost.Domain.Shipping.ShipmentStatus.Booked or ApexPost.Domain.Shipping.ShipmentStatus.Accepted or ApexPost.Domain.Shipping.ShipmentStatus.InTransit);
            }
        }
        if (transactionTask is not null)
        {
            var result = await transactionTask;
            if (result.IsSuccess)
            {
                transactionCount = result.Value.Count;
                openTransactionCount = result.Value.Count(item => item.Status is TransactionStatus.Open or TransactionStatus.PaymentPending);
            }
        }
        if (reportTask is not null)
        {
            var result = await reportTask;
            if (result.IsSuccess) report = result.Value;
        }

        return View("Index", new DashboardViewModel(displayName, role, branchName, environment.EnvironmentName, seedOptions.Enabled,
            inventoryCount, lowStockCount, shipmentCount, pendingShipmentCount, transactionCount, openTransactionCount, report));
    }
}
