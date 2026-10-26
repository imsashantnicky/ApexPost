using System.ComponentModel.DataAnnotations;
using ApexPost.Application.Branches;
using ApexPost.Application.Catalogue;
using ApexPost.Application.IdentityVerification;
using ApexPost.Application.MoneyOrders;
using ApexPost.Application.Transactions;
using ApexPost.Domain.IdentityVerification;
using ApexPost.Domain.Payments;

namespace ApexPost.Web.Models;

public sealed class TransactionCreateForm
{
    [Required] public Guid BranchId { get; set; }
    [Required, StringLength(40)] public string CounterReference { get; set; } = "";
    [Required, RegularExpression("^[A-Z]{3}$")] public string CurrencyCode { get; set; } = "INR";
    public IReadOnlyList<BranchOption> Branches { get; set; } = [];
}

public sealed class TransactionLineForm
{
    public Guid TransactionId { get; set; }
    public long ExpectedVersion { get; set; }
    [Required] public Guid ServiceId { get; set; }
    [Required, StringLength(120)] public string Description { get; set; } = "";
    [Range(1, 1000)] public int Quantity { get; set; } = 1;
}

public sealed class TransactionPaymentForm
{
    public Guid TransactionId { get; set; }
    public long ExpectedVersion { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    [StringLength(80)] public string Reference { get; set; } = "Counter payment";
    [Required, RegularExpression("^[a-fA-F0-9]{32}$")] public string IdempotencyKey { get; set; } = "";
}

public sealed class TransactionReasonForm
{
    public Guid TransactionId { get; set; }
    public long ExpectedVersion { get; set; }
    [Required, StringLength(240, MinimumLength = 3)] public string Reason { get; set; } = "";
}

public sealed record TransactionDetailsViewModel(TransactionResponse Transaction, IReadOnlyList<ServiceResponse> Services, bool CanManage, ReceiptResponse? Receipt, string PaymentIdempotencyKey);

public sealed class ShipmentPreviewForm : ShipmentForm
{
    public string ServiceName { get; set; } = "";
}

public sealed class MoneyOrderForm
{
    [Required, StringLength(30)] public string OrderNumber { get; set; } = "";
    [Range(typeof(decimal), "0.01", "999999999")] public decimal Amount { get; set; }
    [Required, RegularExpression("^[A-Z]{3}$")] public string CurrencyCode { get; set; } = "INR";
    [Required] public Guid BranchId { get; set; }
    [Required, StringLength(120)] public string BeneficiaryName { get; set; } = "";
    [Required, StringLength(80)] public string BeneficiaryReference { get; set; } = "";
    public Guid? IdentityVerificationId { get; set; }
    public IReadOnlyList<BranchOption> Branches { get; set; } = [];
    public decimal VerificationThreshold { get; set; }
}

public sealed class IdentityVerificationStartForm
{
    [Required] public Guid BranchId { get; set; }
    [Required] public Guid SubjectId { get; set; }
    public VerificationMethod Method { get; set; } = VerificationMethod.InPersonReview;
    public IReadOnlyList<BranchOption> Branches { get; set; } = [];
}

public sealed class IdentityVerificationCompleteForm
{
    public Guid VerificationId { get; set; }
    public long ExpectedVersion { get; set; }
    public VerificationMethod Method { get; set; }
    public string Status { get; set; } = "Pending";
    public bool Verified { get; set; }
    [StringLength(240)] public string? RejectionReason { get; set; }
}

public sealed record MoneyOrderDetailsViewModel(MoneyOrderResponse MoneyOrder);
public sealed record StaffIndexRow(ApexPost.Application.Staff.StaffResponse Staff, string BranchName);
