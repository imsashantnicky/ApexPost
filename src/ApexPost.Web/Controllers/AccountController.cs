using System.Security.Claims;
using ApexPost.Web.Models;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;

namespace ApexPost.Web.Controllers;
public sealed class AccountController(LocalAccountService accounts) : Controller
{
    [HttpGet] public async Task<IActionResult> Bootstrap(CancellationToken token) => await accounts.HasAccountsAsync(token) ? NotFound() : View(new BootstrapViewModel());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Bootstrap(BootstrapViewModel model, CancellationToken token) { if (!ModelState.IsValid || !await accounts.BootstrapAsync(model.UserName, model.Password, token)) { ModelState.AddModelError("", "Bootstrap could not be completed."); return View(model); } return RedirectToAction(nameof(Login)); }
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

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, account.UserName),
            new Claim(ClaimTypes.Name, account.UserName),
            new Claim(ClaimTypes.Role, account.Role)
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
        return RedirectToAction("Index", "Dashboard");
    }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Logout() { await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); return RedirectToAction(nameof(Login)); }
    [HttpGet] public IActionResult AccessDenied() => View();
    [Authorize(Policy = "AdministratorOnly")]
    [HttpGet] public IActionResult Provision() => View(new ProvisionAccountViewModel());
    [Authorize(Policy = "AdministratorOnly")]
    [HttpPost] public async Task<IActionResult> Provision(ProvisionAccountViewModel model, CancellationToken token)
    {
        if (!ModelState.IsValid) return View(model);
        if (!await accounts.CreateAsync(model.UserName, model.Password, model.Role, token))
        {
            ModelState.AddModelError("", "The account could not be created. The user name may already be in use.");
            return View(model);
        }

        TempData["Status"] = "Account provisioned.";
        return RedirectToAction(nameof(Provision));
    }
}
