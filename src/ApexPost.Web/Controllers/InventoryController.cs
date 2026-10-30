using ApexPost.Web.Security;
using ApexPost.Application.Inventory;
using ApexPost.Web.Extensions;
using ApexPost.Web.Models;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = Policies.InventoryOperations)]
public sealed class InventoryController(InventoryOperationsUseCase useCase, BranchSelectionService branchSelection, TimeProvider timeProvider) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token)
    {
        var result = await useCase.ListAsync(token);
        return result.IsSuccess ? View(result.Value) : ApplicationResultHttp.Failure(this, result.Errors);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken token) => View(await PopulateAsync(new InventoryForm(), token));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InventoryForm model, CancellationToken token)
    {
        branchSelection.ApplyActorBranch(model);
        if (!ModelState.IsValid || model.BranchId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(model.BranchId), "Select an active branch.");
            await PopulateAsync(model, token);
            return View(model);
        }
        var result = await useCase.CreateAsync(new(model.BranchId, model.Sku, model.Name, model.ReorderLevel), token);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, "The inventory item could not be created. Check the SKU and branch access.");
            await PopulateAsync(model, token);
            return View(model);
        }
        TempData["Status"] = "Inventory item created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(Guid id, long version, int quantity, string reason, CancellationToken token)
    {
        var result = await useCase.ReceiveAsync(new(id, version, quantity, reason, timeProvider.GetUtcNow()), token);
        if (result.IsSuccess) TempData["Status"] = "Stock received.";
        else TempData["Error"] = "The inventory operation could not be completed. Check quantity, reason, and current stock version.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(Guid id, long version, int quantity, string reason, CancellationToken token)
    {
        var result = await useCase.IssueAsync(new(id, version, quantity, reason, timeProvider.GetUtcNow()), token);
        if (result.IsSuccess) TempData["Status"] = "Stock issued.";
        else TempData["Error"] = "The inventory operation could not be completed. Check quantity, reason, and available stock.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<InventoryForm> PopulateAsync(InventoryForm model, CancellationToken token)
    {
        await branchSelection.PopulateAsync(model, token);
        return model;
    }
}
