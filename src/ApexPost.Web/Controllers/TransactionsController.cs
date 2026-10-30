using ApexPost.Web.Security;
using ApexPost.Application.Catalogue;
using ApexPost.Application.Security;
using ApexPost.Application.Transactions;
using ApexPost.Web.Extensions;
using ApexPost.Web.Models;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = Policies.CounterOperations)]
public sealed class TransactionsController(
    TransactionOperationsUseCase useCase,
    CatalogueManagementUseCase catalogue,
    BranchSelectionService branchSelection,
    ICurrentActor currentActor) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token)
    {
        var result = await useCase.ListAsync(token);
        return result.IsSuccess ? View(result.Value) : ApplicationResultHttp.Failure(this, result.Errors);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken token)
    {
        var model = new TransactionCreateForm { CurrencyCode = "INR" };
        await branchSelection.PopulateAsync(model, token);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TransactionCreateForm model, CancellationToken token)
    {
        branchSelection.ApplyActorBranch(model);
        if (!ModelState.IsValid || model.BranchId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(model.BranchId), "Select an available branch.");
            await branchSelection.PopulateAsync(model, token);
            return View(model);
        }
        var result = await useCase.CreateAsync(new(model.BranchId, model.CounterReference, model.CurrencyCode), token);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, SafeFailure(result.Errors));
            await branchSelection.PopulateAsync(model, token);
            return View(model);
        }
        TempData["Status"] = "Counter transaction opened.";
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken token)
    {
        var result = await useCase.GetAsync(id, token);
        if (!result.IsSuccess) return ApplicationResultHttp.Failure(this, result.Errors);
        var services = (await catalogue.ListAsync(token)).Where(item => item.Availability == ApexPost.Domain.Catalogue.ServiceAvailability.Available).ToArray();
        var role = currentActor.Actor?.Role;
        var receipt = result.Value.Status == ApexPost.Domain.Transactions.TransactionStatus.Paid
            ? (await useCase.GetReceiptAsync(id, token)).Value
            : null;
        return View(new TransactionDetailsViewModel(result.Value, services, StaffRoleMapping.Allows(role, OperationalCapability.BranchManager), receipt, Guid.NewGuid().ToString("N")));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLine(TransactionLineForm model, CancellationToken token)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Enter a service, description, and quantity greater than zero.";
            return RedirectToAction(nameof(Details), new { id = model.TransactionId });
        }
        var result = await useCase.AddLineAsync(new(model.TransactionId, model.ExpectedVersion, model.ServiceId, model.Description, model.Quantity), token);
        if (!result.IsSuccess) TempData["Error"] = SafeFailure(result.Errors);
        else TempData["Status"] = "Basket line added.";
        return RedirectToAction(nameof(Details), new { id = model.TransactionId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StartPayment(TransactionPaymentForm model, CancellationToken token)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Reference))
        {
            TempData["Error"] = "Enter a payment reference. Do not enter card numbers or security codes.";
            return RedirectToAction(nameof(Details), new { id = model.TransactionId });
        }
        var transaction = await useCase.GetAsync(model.TransactionId, token);
        if (!transaction.IsSuccess) return ApplicationResultHttp.Failure(this, transaction.Errors);
        var request = new StartPaymentRequest(model.TransactionId, model.ExpectedVersion, transaction.Value.Total, model.Method, model.Reference.Trim(), model.IdempotencyKey);
        var result = await useCase.StartPaymentAsync(request, token);
        if (!result.IsSuccess) TempData["Error"] = SafeFailure(result.Errors);
        else TempData["Status"] = "Payment started. Confirm or decline it after completing the payment at the counter.";
        return RedirectToAction(nameof(Details), new { id = model.TransactionId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment(Guid id, long version, CancellationToken token)
    {
        var result = await useCase.ConfirmPaymentAsync(id, version, token);
        SetOutcome(result.IsSuccess, result.Errors, "Payment confirmed.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeclinePayment(Guid id, long version, CancellationToken token)
    {
        var result = await useCase.DeclinePaymentAsync(id, version, "Payment was not completed at the counter.", token);
        SetOutcome(result.IsSuccess, result.Errors, "Payment declined.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(TransactionReasonForm model, CancellationToken token)
    {
        if (!ModelState.IsValid) TempData["Error"] = "A reason of at least 3 characters is required.";
        else
        {
            var result = await useCase.CancelAsync(model.TransactionId, model.ExpectedVersion, model.Reason, token);
            SetOutcome(result.IsSuccess, result.Errors, "Transaction voided.");
        }
        return RedirectToAction(nameof(Details), new { id = model.TransactionId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Refund(TransactionReasonForm model, CancellationToken token)
    {
        if (!ModelState.IsValid) TempData["Error"] = "A reason of at least 3 characters is required.";
        else
        {
            var result = await useCase.ReversePaymentAsync(model.TransactionId, model.ExpectedVersion, model.Reason, token);
            SetOutcome(result.IsSuccess, result.Errors, "Refund recorded.");
        }
        return RedirectToAction(nameof(Details), new { id = model.TransactionId });
    }


    private void SetOutcome(bool succeeded, IReadOnlyList<ApexPost.Application.Results.ValidationError> errors, string successMessage)
    {
        if (succeeded) TempData["Status"] = successMessage;
        else TempData["Error"] = SafeFailure(errors);
    }

    private static string SafeFailure(IReadOnlyList<ApexPost.Application.Results.ValidationError> errors) =>
        errors.Any(error => error.Code == "access.denied" || error.Code.StartsWith("actor.", StringComparison.Ordinal) || error.Code.StartsWith("branch.", StringComparison.Ordinal))
            ? "You are not authorized to perform this operation at that branch."
            : "The transaction could not be completed. Check the entry and current status, then try again.";
}
