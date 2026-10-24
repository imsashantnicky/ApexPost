using ApexPost.Application.Staff;
using ApexPost.Domain.Staff;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;
[Authorize(Policy = "Management")]
public sealed class StaffController(StaffManagementUseCase useCase) : Controller
{
    public async Task<IActionResult> Index(CancellationToken token) => View(await useCase.ListAsync(token));
    [HttpGet] public IActionResult Create() => View(new StaffForm());
    [HttpPost] public async Task<IActionResult> Create(StaffForm model, CancellationToken token) { if (!ModelState.IsValid || !Enum.TryParse<StaffRole>(model.Role, out var role)) { ModelState.AddModelError("", "A valid staff role is required."); return View(model); } var r = await useCase.CreateAsync(new(model.EmployeeNumber, model.Name, model.BranchId, [role]), token); if (!r.IsSuccess) { ModelState.AddModelError("", r.Errors[0].Message); return View(model); } return RedirectToAction(nameof(Index)); }
    [HttpGet] public async Task<IActionResult> Edit(Guid id, CancellationToken token) { var r = await useCase.GetAsync(id, token); return !r.IsSuccess ? NotFound() : View(new StaffForm { Id = r.Value.Id, Version = r.Value.Version, EmployeeNumber = r.Value.EmployeeNumber, Name = r.Value.Name, BranchId = r.Value.BranchId, Role = r.Value.Roles.First().ToString() }); }
    [HttpPost] public async Task<IActionResult> Edit(StaffForm model, CancellationToken token) { if (!ModelState.IsValid || !Enum.TryParse<StaffRole>(model.Role, out var role)) { ModelState.AddModelError("", "A valid staff role is required."); return View(model); } var r = await useCase.UpdateAsync(new(model.Id, model.Version, model.Name, model.BranchId, [role]), token); if (!r.IsSuccess) { ModelState.AddModelError("", r.Errors[0].Message); return View(model); } return RedirectToAction(nameof(Index)); }
    [HttpPost] public async Task<IActionResult> Deactivate(Guid id, long version, CancellationToken token) { var r = await useCase.DeactivateAsync(id, version, token); if (!r.IsSuccess) TempData["Error"] = r.Errors[0].Message; return RedirectToAction(nameof(Index)); }
}
