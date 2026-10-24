using ApexPost.Application.Shipping;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = "Management")]
public sealed class ShipmentsController(ShipmentOperationsUseCase useCase) : Controller
{
    public async Task<IActionResult> Index(CancellationToken token) => View(await useCase.ListAsync(token));
    [HttpGet] public IActionResult Book() => View(new ShipmentForm());
    [HttpPost] public async Task<IActionResult> Book(ShipmentForm model, CancellationToken token)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await useCase.BookAsync(new(model.Number, model.SenderName, model.SenderLine1, model.SenderCity, model.SenderPostalCode, model.SenderCountryCode, model.RecipientName, model.RecipientLine1, model.RecipientCity, model.RecipientPostalCode, model.RecipientCountryCode, model.ServiceId, model.WeightKilograms, model.Contents, model.ChargeAmount, model.CurrencyCode), token);
        if (!result.IsSuccess) { ModelState.AddModelError("", result.Errors[0].Message); return View(model); }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost] public async Task<IActionResult> Advance(Guid id, long version, string action, CancellationToken token)
    {
        var result = action switch { "accept" => await useCase.AcceptAsync(id, version, token), "dispatch" => await useCase.DispatchAsync(id, version, token), "deliver" => await useCase.DeliverAsync(id, version, token), "cancel" => await useCase.CancelAsync(id, version, token), _ => null };
        if (result is null || !result.IsSuccess) TempData["Error"] = result?.Errors[0].Message ?? "Invalid shipment action.";
        return RedirectToAction(nameof(Index));
    }
}
