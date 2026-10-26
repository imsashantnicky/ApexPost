using ApexPost.Application.Staff;
using ApexPost.Application.Branches;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Staff;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;
[Authorize(Policy = "AdministratorOnly")]
public sealed class StaffController(StaffManagementUseCase useCase, IBranchRepository branches) : Controller
{
    public async Task<IActionResult> Index(CancellationToken token) { var staff = await useCase.ListAsync(token); var names = (await branches.ListAsync(token)).ToDictionary(item => item.Value.Id.Value, item => item.Value.Name); return View(staff.Select(item => new StaffIndexRow(item, names.GetValueOrDefault(item.BranchId, "Unavailable branch"))).ToArray()); }
    [HttpGet] public async Task<IActionResult> Create(CancellationToken token) => View(await PopulateAsync(new StaffForm(), token));
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Create(StaffForm model, CancellationToken token) { if (!ModelState.IsValid || !Enum.TryParse<StaffRole>(model.Role, out var role)) { ModelState.AddModelError("", "A valid staff role is required."); await PopulateAsync(model, token); return View(model); } var r = await useCase.CreateAsync(new(model.EmployeeNumber, model.Name, model.BranchId, [role]), token); if (!r.IsSuccess) { ModelState.AddModelError("", "Staff member could not be created. Check employee number, branch, and role."); await PopulateAsync(model, token); return View(model); } TempData["Status"] = "Staff member created."; return RedirectToAction(nameof(Index)); }
    [HttpGet] public async Task<IActionResult> Edit(Guid id, CancellationToken token) { var r = await useCase.GetAsync(id, token); if (!r.IsSuccess) return NotFound(); var model = new StaffForm { Id = r.Value.Id, Version = r.Value.Version, EmployeeNumber = r.Value.EmployeeNumber, Name = r.Value.Name, BranchId = r.Value.BranchId, Role = r.Value.Roles.First().ToString() }; await PopulateAsync(model, token); return View(model); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Edit(StaffForm model, CancellationToken token) { if (!ModelState.IsValid || !Enum.TryParse<StaffRole>(model.Role, out var role)) { ModelState.AddModelError("", "A valid staff role is required."); await PopulateAsync(model, token); return View(model); } var r = await useCase.UpdateAsync(new(model.Id, model.Version, model.Name, model.BranchId, [role]), token); if (!r.IsSuccess) { ModelState.AddModelError("", "Staff member could not be updated. It may have changed; reload and retry."); await PopulateAsync(model, token); return View(model); } TempData["Status"] = "Staff member updated."; return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Deactivate(Guid id, long version, CancellationToken token) { var r = await useCase.DeactivateAsync(id, version, token); if (!r.IsSuccess) TempData["Error"] = "Staff member could not be deactivated. Check current status and version."; else TempData["Status"] = "Staff member deactivated."; return RedirectToAction(nameof(Index)); }
    private async Task<StaffForm> PopulateAsync(StaffForm model, CancellationToken token) { model.Branches = (await branches.ListAsync(token)).Where(item => item.Value.Status == BranchStatus.Active).Select(item => new BranchOption(item.Value.Id.Value, item.Value.Name)).OrderBy(item => item.Name).ToArray(); return model; }
}
