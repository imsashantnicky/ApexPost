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
    public IActionResult Index()
    {
        return View(new DashboardViewModel(environment.EnvironmentName, seedOptions.Enabled));
    }
}
