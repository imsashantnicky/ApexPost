using ApexPost.Application.MoneyOrders;
using ApexPost.Web.Extensions;
using ApexPost.Web.Middleware;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApexPost.Web.Controllers;

[ApiController]
[Route("api/integrations/money-orders")]
[Authorize(Policy = "IntegrationCounter", AuthenticationSchemes = IntegrationApiKeyDefaults.Scheme)]
[EnableRateLimiting("partner")]
[IntegrationIpRestricted]
[IgnoreAntiforgeryToken]
public sealed class IntegrationMoneyOrdersController(MoneyOrderOperationsUseCase useCase) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Issue([FromBody] IssueMoneyOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await useCase.IssueAsync(request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ApplicationResultHttp.Failure(this, result.Errors);
    }
}
