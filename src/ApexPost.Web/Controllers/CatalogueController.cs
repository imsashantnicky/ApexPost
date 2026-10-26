using ApexPost.Application.Catalogue;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;
[Authorize(Policy = "AdministratorOnly")]
public sealed class CatalogueController(CatalogueManagementUseCase useCase) : Controller
{
    public async Task<IActionResult> Index(CancellationToken token) => View(await useCase.ListAsync(token));
    [HttpGet] public IActionResult Create() => View(new ServiceForm());
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Create(ServiceForm model, CancellationToken token) { if (!ModelState.IsValid) return View(model); var r = await useCase.CreateAsync(new(model.Code, model.Name, model.Description), token); if (!r.IsSuccess) { ModelState.AddModelError("", "Service could not be created. Check the service details."); return View(model); } TempData["Status"] = "Service created."; return RedirectToAction(nameof(Index)); }
    [HttpGet] public async Task<IActionResult> Edit(Guid id, CancellationToken token) { var r = await useCase.GetAsync(id, token); return !r.IsSuccess ? NotFound() : View(new ServiceForm { Id = r.Value.Id, Version = r.Value.Version, Code = r.Value.Code, Name = r.Value.Name, Description = r.Value.Description }); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Edit(ServiceForm model, CancellationToken token) { if (!ModelState.IsValid) return View(model); var r = await useCase.UpdateAsync(new(model.Id, model.Version, model.Name, model.Description), token); if (!r.IsSuccess) { ModelState.AddModelError("", "Service could not be updated. It may have changed; reload and retry."); return View(model); } TempData["Status"] = "Service updated."; return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Deactivate(Guid id, long version, CancellationToken token) { var r = await useCase.DeactivateAsync(id, version, token); if (!r.IsSuccess) TempData["Error"] = "Service could not be deactivated. Check its current status."; else TempData["Status"] = "Service deactivated."; return RedirectToAction(nameof(Index)); }
}
