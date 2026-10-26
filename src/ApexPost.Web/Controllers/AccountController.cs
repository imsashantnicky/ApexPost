using System.Security.Claims;
using ApexPost.Web.Models;
using ApexPost.Web.Services;
using ApexPost.Application.Staff;
using ApexPost.Application.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;

namespace ApexPost.Web.Controllers;
public sealed class AccountController(LocalAccountService accounts, IStaffRepository staffRepository, IBranchRepository branchRepository) : Controller
{
    [HttpGet] public async Task<IActionResult> Bootstrap(CancellationToken token) => await accounts.HasAccountsAsync(token) ? NotFound() : View(await PopulateBootstrapStaffAsync(new BootstrapViewModel(), token));
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Bootstrap(BootstrapViewModel model, CancellationToken token)
    {
        var staff = model.StaffId == Guid.Empty ? null : await staffRepository.GetByIdAsync(EntityId.From(model.StaffId), token);
        if (!ModelState.IsValid || staff is null || staff.Value.Status != EmploymentStatus.Active
            || !await accounts.BootstrapAsync(model.UserName, model.Password, staff.Value.Id.Value, staff.Value.BranchId.Value, token))
        {
            ModelState.AddModelError("", "Bootstrap could not be completed. Select an active staff member.");
            return View(await PopulateBootstrapStaffAsync(model, token));
        }
        return RedirectToAction(nameof(Login));
    }
    [HttpGet] public IActionResult Login() => View(new LoginViewModel());
    [HttpPost, EnableRateLimiting("login"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken token)
    {
        if (!ModelState.IsValid) return View(model);

        var account = await accounts.ValidateAsync(model.UserName, model.Password, token);
        if (account is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid user name or password.");
            return View(model);
        }

        if (account.StaffId is not Guid staffId || staffId == Guid.Empty)
        {
            ModelState.AddModelError(string.Empty, "Invalid user name or password.");
            return View(model);
        }
        var linkedStaff = await staffRepository.GetByIdAsync(EntityId.From(staffId), token);
        if (linkedStaff is null || linkedStaff.Value.Status != EmploymentStatus.Active || !RoleMatchesStaff(account.Role, linkedStaff.Value.Roles))
        {
            ModelState.AddModelError(string.Empty, "Invalid user name or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.UserName),
            new(ClaimTypes.Name, account.UserName),
            new(ClaimTypes.Role, LocalAccountService.NormalizeRole(account.Role))
        };
        claims.Add(new Claim(ActorClaimTypes.StaffId, linkedStaff.Value.Id.Value.ToString("D")));
        claims.Add(new Claim(ActorClaimTypes.AssignedBranchId, linkedStaff.Value.BranchId.Value.ToString("D")));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
        return RedirectToAction(LocalAccountService.GetDashboardAction(account.Role), "Dashboard");
    }
    [Authorize, HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return RedirectToAction(nameof(Login)); }
    [HttpGet] public IActionResult AccessDenied() => View();
    [Authorize(Policy = "AdministratorOnly")]
    [HttpGet] public async Task<IActionResult> Provision(CancellationToken token) => View(await PopulateStaffAsync(new ProvisionAccountViewModel(), token));
    [Authorize(Policy = "AdministratorOnly")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Provision(ProvisionAccountViewModel model, CancellationToken token)
    {
        var staff = model.StaffId == Guid.Empty ? null : await staffRepository.GetByIdAsync(EntityId.From(model.StaffId), token);
        var roleMatches = staff is not null && staff.Value.Status == EmploymentStatus.Active && RoleMatchesStaff(model.Role, staff.Value.Roles);
        if (!ModelState.IsValid || !roleMatches)
        {
            ModelState.AddModelError("", "Select an active staff member with a compatible role.");
            return View(await PopulateStaffAsync(model, token));
        }
        if (!await accounts.CreateAsync(model.UserName, model.Password, model.Role, staff!.Value.Id.Value, staff.Value.BranchId.Value, token))
        {
            ModelState.AddModelError("", "The account could not be created. The user name may already be in use.");
            return View(await PopulateStaffAsync(model, token));
        }

        TempData["Status"] = "Account provisioned.";
        return RedirectToAction(nameof(Provision));
    }

    private async Task<ProvisionAccountViewModel> PopulateStaffAsync(ProvisionAccountViewModel model, CancellationToken token)
    {
        var branches = await branchRepository.ListAsync(token);
        var branchNames = branches.ToDictionary(x => x.Value.Id.Value, x => x.Value.Name);
        var staff = await staffRepository.ListAsync(token);
        model.Staff = staff.Where(x => x.Value.Status == EmploymentStatus.Active)
            .Select(x => new StaffOption(x.Value.Id.Value, x.Value.EmployeeNumber.Value, x.Value.Name, branchNames.GetValueOrDefault(x.Value.BranchId.Value, "Unavailable branch")))
            .OrderBy(x => x.EmployeeNumber).ToArray();
        return model;
    }

    private async Task<BootstrapViewModel> PopulateBootstrapStaffAsync(BootstrapViewModel model, CancellationToken token)
    {
        var branches = await branchRepository.ListAsync(token);
        var branchNames = branches.ToDictionary(x => x.Value.Id.Value, x => x.Value.Name);
        var staff = await staffRepository.ListAsync(token);
        model.Staff = staff.Where(x => x.Value.Status == EmploymentStatus.Active)
            .Select(x => new StaffOption(x.Value.Id.Value, x.Value.EmployeeNumber.Value, x.Value.Name, branchNames.GetValueOrDefault(x.Value.BranchId.Value, "Unavailable branch")))
            .OrderBy(x => x.EmployeeNumber).ToArray();
        return model;
    }

    private static bool RoleMatchesStaff(string role, IReadOnlyCollection<StaffRole> staffRoles) => LocalAccountService.NormalizeRole(role) switch
    {
        LocalAccountService.AdministratorRole or LocalAccountService.AuditorRole => true,
        LocalAccountService.BranchManagerRole => staffRoles.Contains(StaffRole.BranchManager),
        LocalAccountService.CounterStaffRole => staffRoles.Contains(StaffRole.CounterClerk),
        LocalAccountService.InventoryStaffRole => staffRoles.Contains(StaffRole.InventoryClerk),
        _ => false
    };
}
