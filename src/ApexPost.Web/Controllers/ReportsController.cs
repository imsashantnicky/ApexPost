using ApexPost.Application.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = "ReportsAccess")]
public sealed class ReportsController(OperationalReportService reportService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token)
    {
        var report = await reportService.BuildAsync(token);
        return View(report);
    }
}
