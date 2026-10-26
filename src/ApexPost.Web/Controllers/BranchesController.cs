using ApexPost.Application.Branches;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;
[Authorize(Policy = "AdministratorOnly")]
public sealed class BranchesController(BranchManagementUseCase useCase) : Controller
{
    public async Task<IActionResult> Index(CancellationToken token) => View(await useCase.ListAsync(token));
    [HttpGet] public IActionResult Create() => View(new BranchForm());
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Create(BranchForm model, CancellationToken token) { if (!ModelState.IsValid) return View(model); var r = await useCase.CreateAsync(new(model.Code, model.Name, model.AddressLine1, model.AddressLine2, model.City, model.PostalCode, model.CountryCode, model.CounterCount), token); if (!r.IsSuccess) { ModelState.AddModelError("", "Branch could not be created. Check the branch details."); return View(model); } TempData["Status"] = "Branch created."; return RedirectToAction(nameof(Index)); }
    [HttpGet] public async Task<IActionResult> Edit(Guid id, CancellationToken token) { var r = await useCase.GetAsync(id, token); return !r.IsSuccess ? NotFound() : View(new BranchForm { Id = r.Value.Id, Version = r.Value.Version, Code = r.Value.Code, Name = r.Value.Name, AddressLine1 = r.Value.AddressLine1, AddressLine2 = r.Value.AddressLine2, City = r.Value.City, PostalCode = r.Value.PostalCode, CountryCode = r.Value.CountryCode, CounterCount = r.Value.CounterCount }); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Edit(BranchForm model, CancellationToken token) { if (!ModelState.IsValid) return View(model); var r = await useCase.UpdateAsync(new(model.Id, model.Version, model.Name, model.AddressLine1, model.AddressLine2, model.City, model.PostalCode, model.CountryCode, model.CounterCount), token); if (!r.IsSuccess) { ModelState.AddModelError("", "Branch could not be updated. It may have changed; reload and retry."); return View(model); } TempData["Status"] = "Branch updated."; return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Deactivate(Guid id, long version, CancellationToken token) { var r = await useCase.DeactivateAsync(id, version, token); if (!r.IsSuccess) TempData["Error"] = "Branch could not be deactivated. Check current status and version."; else TempData["Status"] = "Branch deactivated."; return RedirectToAction(nameof(Index)); }
}
