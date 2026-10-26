using ApexPost.Application.Branches;
using ApexPost.Application.Inventory;
using ApexPost.Application.Security;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Web.Extensions;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = "InventoryOperations")]
public sealed class InventoryController(InventoryOperationsUseCase useCase, ICurrentActor currentActor, IBranchRepository branches) : Controller
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
        ApplyActorBranch(model);
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
        var result = await useCase.ReceiveAsync(new(id, version, quantity, reason, DateTimeOffset.UtcNow), token);
        if (result.IsSuccess) TempData["Status"] = "Stock received.";
        else TempData["Error"] = "The inventory operation could not be completed. Check quantity, reason, and current stock version.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(Guid id, long version, int quantity, string reason, CancellationToken token)
    {
        var result = await useCase.IssueAsync(new(id, version, quantity, reason, DateTimeOffset.UtcNow), token);
        if (result.IsSuccess) TempData["Status"] = "Stock issued.";
        else TempData["Error"] = "The inventory operation could not be completed. Check quantity, reason, and available stock.";
        return RedirectToAction(nameof(Index));
    }

    private void ApplyActorBranch(InventoryForm model)
    {
        if (currentActor.Actor?.Role != "Administrator" && currentActor.Actor?.AssignedBranchId is Guid branchId)
            model.BranchId = branchId;
    }

    private async Task<InventoryForm> PopulateAsync(InventoryForm model, CancellationToken token)
    {
        if (currentActor.Actor?.Role == "Administrator")
            model.Branches = (await branches.ListAsync(token)).Where(item => item.Value.Status == BranchStatus.Active)
                .Select(item => new BranchOption(item.Value.Id.Value, item.Value.Name)).OrderBy(item => item.Name).ToArray();
        else if (currentActor.Actor?.AssignedBranchId is Guid branchId)
        {
            var branch = await branches.GetByIdAsync(EntityId.From(branchId), token);
            model.Branches = branch is not null && branch.Value.Status == BranchStatus.Active ? [new BranchOption(branchId, branch.Value.Name)] : [];
            model.BranchId = branchId;
        }
        return model;
    }
}
