using ApexPost.Application.Inventory;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = "Management")]
public sealed class InventoryController(InventoryOperationsUseCase useCase) : Controller
{
    public async Task<IActionResult> Index(CancellationToken token) => View(await useCase.ListAsync(token));
    [HttpGet] public IActionResult Create() => View(new InventoryForm());
    [HttpPost] public async Task<IActionResult> Create(InventoryForm model, CancellationToken token)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await useCase.CreateAsync(new(model.BranchId, model.Sku, model.Name, model.ReorderLevel), token);
        if (!result.IsSuccess) { ModelState.AddModelError("", result.Errors[0].Message); return View(model); }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> Receive(Guid id, long version, int quantity, string reason, CancellationToken token)
    {
        var result = await useCase.ReceiveAsync(new(id, version, quantity, reason, DateTimeOffset.UtcNow), token);
        if (!result.IsSuccess) TempData["Error"] = result.Errors[0].Message;
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> Issue(Guid id, long version, int quantity, string reason, CancellationToken token)
    {
        var result = await useCase.IssueAsync(new(id, version, quantity, reason, DateTimeOffset.UtcNow), token);
        if (!result.IsSuccess) TempData["Error"] = result.Errors[0].Message;
        return RedirectToAction(nameof(Index));
    }
}
