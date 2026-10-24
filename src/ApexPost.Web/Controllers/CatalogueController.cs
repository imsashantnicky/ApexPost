using ApexPost.Application.Catalogue;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;
[Authorize(Policy = "Management")]
public sealed class CatalogueController(CatalogueManagementUseCase useCase) : Controller
{
    public async Task<IActionResult> Index(CancellationToken token) => View(await useCase.ListAsync(token));
    [HttpGet] public IActionResult Create() => View(new ServiceForm());
    [HttpPost] public async Task<IActionResult> Create(ServiceForm model, CancellationToken token) { if (!ModelState.IsValid) return View(model); var r = await useCase.CreateAsync(new(model.Code, model.Name, model.Description), token); if (!r.IsSuccess) { ModelState.AddModelError("", r.Errors[0].Message); return View(model); } return RedirectToAction(nameof(Index)); }
    [HttpGet] public async Task<IActionResult> Edit(Guid id, CancellationToken token) { var r = await useCase.GetAsync(id, token); return !r.IsSuccess ? NotFound() : View(new ServiceForm { Id = r.Value.Id, Version = r.Value.Version, Code = r.Value.Code, Name = r.Value.Name, Description = r.Value.Description }); }
    [HttpPost] public async Task<IActionResult> Edit(ServiceForm model, CancellationToken token) { if (!ModelState.IsValid) return View(model); var r = await useCase.UpdateAsync(new(model.Id, model.Version, model.Name, model.Description), token); if (!r.IsSuccess) { ModelState.AddModelError("", r.Errors[0].Message); return View(model); } return RedirectToAction(nameof(Index)); }
    [HttpPost] public async Task<IActionResult> Deactivate(Guid id, long version, CancellationToken token) { var r = await useCase.DeactivateAsync(id, version, token); if (!r.IsSuccess) TempData["Error"] = r.Errors[0].Message; return RedirectToAction(nameof(Index)); }
}
