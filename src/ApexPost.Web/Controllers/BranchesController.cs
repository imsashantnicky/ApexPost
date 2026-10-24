using ApexPost.Application.Branches;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;
[Authorize(Policy = "Management")]
public sealed class BranchesController(BranchManagementUseCase useCase) : Controller
{
    public async Task<IActionResult> Index(CancellationToken token) => View(await useCase.ListAsync(token));
    [HttpGet] public IActionResult Create() => View(new BranchForm());
    [HttpPost] public async Task<IActionResult> Create(BranchForm model, CancellationToken token) { if (!ModelState.IsValid) return View(model); var r = await useCase.CreateAsync(new(model.Code, model.Name, model.AddressLine1, model.AddressLine2, model.City, model.PostalCode, model.CountryCode, model.CounterCount), token); if (!r.IsSuccess) { ModelState.AddModelError("", r.Errors[0].Message); return View(model); } return RedirectToAction(nameof(Index)); }
    [HttpGet] public async Task<IActionResult> Edit(Guid id, CancellationToken token) { var r = await useCase.GetAsync(id, token); return !r.IsSuccess ? NotFound() : View(new BranchForm { Id = r.Value.Id, Version = r.Value.Version, Code = r.Value.Code, Name = r.Value.Name, AddressLine1 = r.Value.AddressLine1, AddressLine2 = r.Value.AddressLine2, City = r.Value.City, PostalCode = r.Value.PostalCode, CountryCode = r.Value.CountryCode, CounterCount = r.Value.CounterCount }); }
    [HttpPost] public async Task<IActionResult> Edit(BranchForm model, CancellationToken token) { if (!ModelState.IsValid) return View(model); var r = await useCase.UpdateAsync(new(model.Id, model.Version, model.Name, model.AddressLine1, model.AddressLine2, model.City, model.PostalCode, model.CountryCode, model.CounterCount), token); if (!r.IsSuccess) { ModelState.AddModelError("", r.Errors[0].Message); return View(model); } return RedirectToAction(nameof(Index)); }
    [HttpPost] public async Task<IActionResult> Deactivate(Guid id, long version, CancellationToken token) { var r = await useCase.DeactivateAsync(id, version, token); if (!r.IsSuccess) TempData["Error"] = r.Errors[0].Message; return RedirectToAction(nameof(Index)); }
}
