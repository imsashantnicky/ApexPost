using ApexPost.Web.Security;
using ApexPost.Application.IdentityVerification;
using ApexPost.Application.MoneyOrders;
using ApexPost.Application.Security;
using ApexPost.Domain.IdentityVerification;
using ApexPost.Web.Extensions;
using ApexPost.Web.Models;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Controllers;

[Authorize(Policy = Policies.CounterOperations)]
public sealed class MoneyOrdersController(
    MoneyOrderOperationsUseCase moneyOrders,
    IdentityVerificationOperationsUseCase verifications,
    BranchSelectionService branchSelection,
    ICurrentActor currentActor,
    MoneyOrderPolicy policy) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token)
    {
        var result = await moneyOrders.ListAsync(token);
        return result.IsSuccess ? View(result.Value) : ApplicationResultHttp.Failure(this, result.Errors);
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid? verificationId, CancellationToken token)
    {
        var model = new MoneyOrderForm { CurrencyCode = "INR", IdentityVerificationId = verificationId, VerificationThreshold = policy.IdentityVerificationThreshold };
        await branchSelection.PopulateAsync(model, token);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(MoneyOrderForm model, CancellationToken token)
    {
        branchSelection.ApplyActorBranch(model);
        if (!ModelState.IsValid || model.BranchId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(model.BranchId), "Choose an available branch.");
            model.VerificationThreshold = policy.IdentityVerificationThreshold;
            await branchSelection.PopulateAsync(model, token);
            return View("Create", model);
        }
        var request = new IssueMoneyOrderRequest(model.OrderNumber, model.Amount, model.CurrencyCode, model.BranchId,
            model.BeneficiaryName, model.BeneficiaryReference, model.IdentityVerificationId);
        var result = await moneyOrders.IssueAsync(request, token);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, SafeFailure(result.Errors));
            model.VerificationThreshold = policy.IdentityVerificationThreshold;
            await branchSelection.PopulateAsync(model, token);
            return View("Create", model);
        }
        TempData["Status"] = "Money order issued.";
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken token)
    {
        var result = await moneyOrders.GetAsync(id, token);
        return result.IsSuccess ? View(new MoneyOrderDetailsViewModel(result.Value)) : ApplicationResultHttp.Failure(this, result.Errors);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(Guid id, long version, CancellationToken token)
    {
        var result = await moneyOrders.MarkPaidAsync(id, version, token);
        if (result.IsSuccess) TempData["Status"] = "Money order marked paid.";
        else TempData["Error"] = "The money order could not be marked paid. Check its status and branch access.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Verify(Guid? branchId, CancellationToken token)
    {
        var model = new IdentityVerificationStartForm { BranchId = branchId ?? currentActor.Actor?.AssignedBranchId ?? Guid.Empty };
        await branchSelection.PopulateAsync(model, token);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StartVerification(IdentityVerificationStartForm model, CancellationToken token)
    {
        branchSelection.ApplyActorBranch(model);
        if (!ModelState.IsValid || model.BranchId == Guid.Empty || model.SubjectId == Guid.Empty)
        {
            ModelState.AddModelError(string.Empty, "Enter a valid customer reference and branch.");
            await branchSelection.PopulateAsync(model, token);
            return View("Verify", model);
        }
        var result = await verifications.StartAsync(new(model.BranchId, model.SubjectId, model.Method), token);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, SafeFailure(result.Errors));
            await branchSelection.PopulateAsync(model, token);
            return View("Verify", model);
        }
        TempData["Status"] = "Verification started. Record the result only after completing the selected review.";
        return RedirectToAction(nameof(CompleteVerification), new { id = result.Value.Id });
    }

    [HttpGet]
    public async Task<IActionResult> CompleteVerification(Guid id, CancellationToken token)
    {
        var result = await verifications.GetAsync(id, token);
        if (!result.IsSuccess) return ApplicationResultHttp.Failure(this, result.Errors);
        return View(new IdentityVerificationCompleteForm
        {
            VerificationId = result.Value.Id,
            ExpectedVersion = result.Value.Version,
            Method = result.Value.Method,
            Status = result.Value.Status.ToString()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteVerification(IdentityVerificationCompleteForm model, CancellationToken token)
    {
        var result = await verifications.CompleteAsync(new(model.VerificationId, model.ExpectedVersion, model.Verified, model.RejectionReason), token);
        if (!result.IsSuccess)
        {
            TempData["Error"] = SafeFailure(result.Errors);
            return RedirectToAction(nameof(CompleteVerification), new { id = model.VerificationId });
        }
        if (model.Verified && result.Value.Status == VerificationStatus.Verified)
        {
            TempData["Status"] = "Identity verification succeeded. Continue issuing the money order.";
            return RedirectToAction(nameof(Create), new { verificationId = result.Value.Id });
        }
        TempData["Error"] = "Identity verification was not successful. A high-value money order cannot be issued.";
        return RedirectToAction(nameof(Create));
    }

    private static string SafeFailure(IReadOnlyList<ApexPost.Application.Results.ValidationError> errors) =>
        errors.Any(error => error.Code == "access.denied" || error.Code.StartsWith("actor.", StringComparison.Ordinal) || error.Code.StartsWith("branch.", StringComparison.Ordinal))
            ? "You are not authorized to perform this operation at that branch."
            : "The operation could not be completed. Check the details and current status, then try again.";
}
