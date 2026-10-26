using ApexPost.Domain.Common;
using ApexPost.Domain.Payments;

namespace ApexPost.Domain.Transactions;

public sealed class RetailTransaction
{
    private readonly List<TransactionLineItem> lineItems = [];
    private readonly List<PaymentRecord> payments = [];

    public RetailTransaction(EntityId id, EntityId branchId, string counterReference, string currencyCode, DateTimeOffset? createdAtUtc = null)
    {
        Id = id ?? throw new DomainException("Transaction identity is required.");
        BranchId = branchId ?? throw new DomainException("Branch identity is required.");
        CounterReference = RequiredCounterReference(counterReference);
        CurrencyCode = ApexPost.Domain.Common.CurrencyCode.From(currencyCode).Value;
        CreatedAtUtc = createdAtUtc?.ToUniversalTime();
        Status = TransactionStatus.Open;
    }

    public EntityId Id { get; }

    public EntityId BranchId { get; }

    public string CounterReference { get; }

    public string CurrencyCode { get; }

    public DateTimeOffset? CreatedAtUtc { get; }

    public TransactionStatus Status { get; private set; }

    public string? VoidReason { get; private set; }

    public IReadOnlyList<TransactionLineItem> LineItems => lineItems.AsReadOnly();

    public IReadOnlyList<PaymentRecord> Payments => payments.AsReadOnly();

    public decimal Total => lineItems.Sum(lineItem => lineItem.Total);

    public TransactionLineItem AddLineItem(EntityId serviceId, string description, int quantity, decimal unitPrice, TransactionLineKind kind = TransactionLineKind.Service)
    {
        EnsureOpen();
        var lineItem = new TransactionLineItem(
            EntityId.New(),
            serviceId ?? throw new DomainException("Service identity is required."),
            description,
            quantity,
            unitPrice,
            kind);
        lineItems.Add(lineItem);
        return lineItem;
    }

    public void RemoveLineItem(EntityId lineItemId)
    {
        EnsureOpen();
        var lineItem = lineItems.SingleOrDefault(item => item.Id == lineItemId);

        if (lineItem is null)
        {
            throw new DomainException("Transaction line item was not found.");
        }

        lineItems.Remove(lineItem);
    }

    public PaymentRecord StartPayment(decimal amount, PaymentMethod method, string reference)
    {
        EnsureOpen();

        if (lineItems.Count == 0)
        {
            throw new DomainException("A transaction must contain at least one line item before payment.");
        }

        if (amount != Total)
        {
            throw new DomainException("Payment amount must equal the transaction total.");
        }

        var payment = new PaymentRecord(EntityId.New(), Id, amount, CurrencyCode, method, reference);
        payments.Add(payment);
        Status = TransactionStatus.PaymentPending;
        return payment;
    }

    public void ConfirmPayment()
    {
        var payment = PendingPayment();
        payment.Confirm();
        Status = TransactionStatus.Paid;
    }

    public void DeclinePayment(string reason)
    {
        var payment = PendingPayment();
        payment.Decline(reason);
        Status = TransactionStatus.Open;
    }

    public void Cancel(string reason)
    {
        var normalizedReason = RequiredReason(reason, "Void reason");
        if (Status == TransactionStatus.PaymentPending)
        {
            PendingPayment().Cancel();
        }
        else if (Status != TransactionStatus.Open)
        {
            throw new DomainException("Only an open or payment-pending transaction can be cancelled.");
        }

        Status = TransactionStatus.Cancelled;
        VoidReason = normalizedReason;
    }

    public void ReversePayment(string reason)
    {
        if (Status != TransactionStatus.Paid)
        {
            throw new DomainException("Only a paid transaction can be reversed.");
        }

        var payment = payments.Single(paymentRecord => paymentRecord.Status == PaymentStatus.Confirmed);
        payment.Reverse(reason);
        Status = TransactionStatus.Reversed;
    }

    private PaymentRecord PendingPayment()
    {
        if (Status != TransactionStatus.PaymentPending)
        {
            throw new DomainException("Transaction does not have a pending payment.");
        }

        return payments.Single(payment => payment.Status == PaymentStatus.Pending);
    }

    private void EnsureOpen()
    {
        if (Status != TransactionStatus.Open)
        {
            throw new DomainException("Only an open transaction can be changed.");
        }
    }

    private static string RequiredCounterReference(string counterReference)
    {
        var normalizedReference = counterReference?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedReference) || normalizedReference.Length > 30)
        {
            throw new DomainException("Counter reference is required and must not exceed 30 characters.");
        }

        return normalizedReference;
    }

    private static string RequiredReason(string reason, string field)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 250) throw new DomainException($"{field} is required and must not exceed 250 characters.");
        return normalized;
    }

}
