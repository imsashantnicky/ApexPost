using ApexPost.Web.Security;
using ApexPost.Application.Catalogue;
using ApexPost.Application.Shipping;
using ApexPost.Application.Transactions;
using ApexPost.Domain.Catalogue;
using ApexPost.Web.Extensions;
using ApexPost.Web.Models;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = Policies.CounterOperations)]
public sealed class ShipmentsController(
    ShipmentOperationsUseCase useCase,
    BranchSelectionService branchSelection,
    CatalogueManagementUseCase catalogue,
    IShippingRateLookup rates) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token)
    {
        var result = await useCase.ListAsync(token);
        return result.IsSuccess ? View(result.Value) : ApplicationResultHttp.Failure(this, result.Errors);
    }

    [HttpGet]
    public async Task<IActionResult> Book(CancellationToken token) => View(await PopulateAsync(new ShipmentForm(), token));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(ShipmentForm model, CancellationToken token)
    {
        branchSelection.ApplyActorBranch(model);
        await PopulateAsync(model, token);
        if (!ModelState.IsValid || model.BranchId == Guid.Empty || !model.Services.Any(service => service.Id == model.ServiceId && service.Availability == ServiceAvailability.Available))
        {
            ModelState.AddModelError(string.Empty, "Review the shipment details and choose an available service.");
            return View("Book", model);
        }
        var price = await rates.GetCurrentRateAsync(model.ZoneCode, model.ServiceLevel, model.CurrencyCode, token);
        if (price is null)
        {
            ModelState.AddModelError(nameof(model.ServiceId), "The selected service has no current price for this currency.");
            return View("Book", model);
        }
        model.ChargeAmount = price.Value;
        var serviceName = model.Services.First(service => service.Id == model.ServiceId).Name;
        return View("Preview", new ShipmentPreviewForm
        {
            BranchId = model.BranchId, Number = model.Number, ServiceId = model.ServiceId, ServiceName = serviceName,
            SenderName = model.SenderName, SenderLine1 = model.SenderLine1, SenderCity = model.SenderCity,
            SenderPostalCode = model.SenderPostalCode, SenderCountryCode = model.SenderCountryCode,
            RecipientName = model.RecipientName, RecipientLine1 = model.RecipientLine1, RecipientCity = model.RecipientCity,
            RecipientPostalCode = model.RecipientPostalCode, RecipientCountryCode = model.RecipientCountryCode,
            WeightKilograms = model.WeightKilograms, Contents = model.Contents, ChargeAmount = price.Value,
            CurrencyCode = model.CurrencyCode, ZoneCode = model.ZoneCode, ServiceLevel = model.ServiceLevel
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(ShipmentForm model, CancellationToken token)
    {
        branchSelection.ApplyActorBranch(model);
        if (!ModelState.IsValid || model.BranchId == Guid.Empty)
        {
            await PopulateAsync(model, token);
            return View("Book", model);
        }
        var price = await rates.GetCurrentRateAsync(model.ZoneCode, model.ServiceLevel, model.CurrencyCode, token);
        if (price is null)
        {
            ModelState.AddModelError(nameof(model.ServiceId), "The selected service is no longer available at this currency price.");
            await PopulateAsync(model, token);
            return View("Book", model);
        }
        var request = new BookShipmentRequest(model.BranchId, model.Number, model.SenderName, model.SenderLine1, model.SenderCity,
            model.SenderPostalCode, model.SenderCountryCode, model.RecipientName, model.RecipientLine1, model.RecipientCity,
            model.RecipientPostalCode, model.RecipientCountryCode, model.ServiceId, model.WeightKilograms, model.Contents,
            price.Value, model.CurrencyCode) with { ZoneCode = model.ZoneCode, ServiceLevel = model.ServiceLevel };
        var result = await useCase.BookAsync(request, token);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, "The shipment could not be booked. Check branch access, package weight, and service availability.");
            await PopulateAsync(model, token);
            return View("Book", model);
        }
        TempData["Status"] = "Shipment booked.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Advance(Guid id, long version, string action, CancellationToken token)
    {
        var result = action switch
        {
            "accept" => await useCase.AcceptAsync(id, version, token),
            "dispatch" => await useCase.DispatchAsync(id, version, token),
            "deliver" => await useCase.DeliverAsync(id, version, token),
            "cancel" => await useCase.CancelAsync(id, version, token),
            _ => null
        };
        if (result is not null && !result.IsSuccess) TempData["Error"] = "The shipment status could not be changed. Check its current status and branch access.";
        else if (result is not null) TempData["Status"] = "Shipment status updated.";
        else TempData["Error"] = "Choose a valid shipment action.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<ShipmentForm> PopulateAsync(ShipmentForm model, CancellationToken token)
    {
        await branchSelection.PopulateAsync(model, token);
        model.Services = (await catalogue.ListAsync(token)).Where(service => service.Availability == ServiceAvailability.Available).ToArray();
        if (model.ServiceId != Guid.Empty)
        {
            var price = await rates.GetCurrentRateAsync(model.ZoneCode, model.ServiceLevel, model.CurrencyCode, token);
            if (price is not null) model.CurrentServicePrice = price.Value;
        }
        model.ZoneCodes = Enumerable.Range(1, 10).Select(zone => $"Z{zone:00}").ToArray();
        return model;
    }
}
