using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize]
[Route("dashboard")]
public sealed class DashboardController : Controller
{
    private readonly IWebHostEnvironment environment;
    private readonly JsonSeedOptions seedOptions;

    public DashboardController(IWebHostEnvironment environment, JsonSeedOptions seedOptions)
    {
        this.environment = environment;
        this.seedOptions = seedOptions;
    }

    [HttpGet("")]
    [HttpGet("index")]
    public IActionResult Index() => ViewDashboard();

    [HttpGet("admin")]
    public IActionResult Admin() => ViewDashboard();

    [HttpGet("branch")]
    public IActionResult Branch() => ViewDashboard();

    [HttpGet("counter")]
    public IActionResult Counter() => ViewDashboard();

    [HttpGet("inventory")]
    public IActionResult Inventory() => ViewDashboard();

    [HttpGet("auditor")]
    public IActionResult Auditor() => ViewDashboard();

    private ViewResult ViewDashboard() => View("Index", new DashboardViewModel(environment.EnvironmentName, seedOptions.Enabled));
}
