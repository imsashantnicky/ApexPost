using ApexPost.Application.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ApexPost.Web.Extensions;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = "ReportsAccess")]
public sealed class ReportsController(OperationalReportService reportService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token)
    {
        var report = await reportService.BuildAsync(token);
        return report.IsSuccess ? View(report.Value) : ApplicationResultHttp.Failure(this, report.Errors);
    }
}
