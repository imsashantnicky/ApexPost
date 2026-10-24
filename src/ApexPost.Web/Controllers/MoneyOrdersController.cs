using ApexPost.Application.MoneyOrders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;
[Authorize(Policy = "Management")]
public sealed class MoneyOrdersController(MoneyOrderOperationsUseCase useCase) : Controller
{
    [HttpPost] public async Task<IActionResult> Issue(IssueMoneyOrderRequest request, CancellationToken token) { var result = await useCase.IssueAsync(request, token); return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors); }
    [HttpPost] public async Task<IActionResult> Pay(Guid id, long version, CancellationToken token) { var result = await useCase.MarkPaidAsync(id, version, token); return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors); }
}
