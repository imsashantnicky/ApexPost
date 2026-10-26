using ApexPost.Domain.Common;

namespace ApexPost.Domain.Payments;

/// <summary>
/// A payment captured at the retail counter. Confirmation represents a local counter action,
/// not confirmation from an external financial provider.
/// </summary>
public sealed class PaymentRecord
{
    internal PaymentRecord(EntityId id, EntityId transactionId, decimal amount, string currencyCode, PaymentMethod method, string reference)
    {
        Id = id;
        TransactionId = transactionId;
        Amount = ValidAmount(amount);
        CurrencyCode = ApexPost.Domain.Common.CurrencyCode.From(currencyCode).Value;

        if (!Enum.IsDefined(method))
        {
            throw new DomainException("Payment method must be recognized.");
        }

        Method = method;
        Reference = RequiredReference(reference);
        Status = PaymentStatus.Pending;
    }

    public EntityId Id { get; }

    public EntityId TransactionId { get; }

    public decimal Amount { get; }

    public string CurrencyCode { get; }

    public PaymentMethod Method { get; }

    public string Reference { get; }

    public PaymentStatus Status { get; private set; }

    public string? DeclineReason { get; private set; }

    public string? ReversalReason { get; private set; }

    internal void Confirm()
    {
        ChangeStatus(PaymentStatus.Confirmed, "Only a pending payment can be confirmed.");
    }

    internal void Decline(string reason)
    {
        var normalizedReason = reason?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > 250)
        {
            throw new DomainException("Payment decline reason is required and must not exceed 250 characters.");
        }

        ChangeStatus(PaymentStatus.Declined, "Only a pending payment can be declined.");
        DeclineReason = normalizedReason;
    }

    internal void Cancel()
    {
        ChangeStatus(PaymentStatus.Cancelled, "Only a pending payment can be cancelled.");
    }

    internal void Reverse(string reason)
    {
        if (Status != PaymentStatus.Confirmed)
        {
            throw new DomainException("Only a confirmed payment can be reversed.");
        }

        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 250) throw new DomainException("Refund reason is required and must not exceed 250 characters.");
        ReversalReason = normalized;
        Status = PaymentStatus.Reversed;
    }

    private void ChangeStatus(PaymentStatus targetStatus, string errorMessage)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new DomainException(errorMessage);
        }

        Status = targetStatus;
    }

    private static decimal ValidAmount(decimal amount)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
        {
            throw new DomainException("Payment amount must be greater than zero and have no more than two decimal places.");
        }

        return amount;
    }

    private static string RequiredReference(string reference)
    {
        var normalizedReference = reference?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedReference) || normalizedReference.Length > 100)
        {
            throw new DomainException("Payment reference is required and must not exceed 100 characters.");
        }

        return normalizedReference;
    }
}
