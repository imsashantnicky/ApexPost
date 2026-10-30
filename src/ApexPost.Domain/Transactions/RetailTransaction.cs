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

    /// <summary>
    /// Restores a persisted transaction exactly as stored: line and payment identities, payment statuses,
    /// transaction status and void reason are taken from the snapshot rather than replayed through
    /// <see cref="StartPayment"/>, so a basket edited after a declined payment reloads correctly.
    /// The snapshot is validated as a whole and rejected when its parts are inconsistent.
    /// </summary>
    public static RetailTransaction Restore(
        EntityId id,
        EntityId branchId,
        string counterReference,
        string currencyCode,
        DateTimeOffset? createdAtUtc,
        TransactionStatus status,
        string? voidReason,
        IEnumerable<TransactionLineState> lines,
        IEnumerable<PaymentState> payments)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(payments);
        if (!Enum.IsDefined(status)) throw new DomainException("Transaction status must be recognized.");

        var transaction = new RetailTransaction(id, branchId, counterReference, currencyCode, createdAtUtc);
        foreach (var line in lines)
        {
            var lineId = line?.Id ?? throw new DomainException("Transaction line identity is required.");
            if (transaction.lineItems.Any(existing => existing.Id == lineId)) throw new DomainException("Transaction line identities must be unique.");
            transaction.lineItems.Add(new TransactionLineItem(lineId, line.ServiceId ?? throw new DomainException("Service identity is required."),
                line.Description, line.Quantity, line.UnitPrice, line.Kind));
        }

        foreach (var payment in payments)
        {
            ArgumentNullException.ThrowIfNull(payment);
            if (transaction.payments.Any(existing => existing.Id == payment.Id)) throw new DomainException("Payment identities must be unique.");
            var record = PaymentRecord.Restore(transaction.Id, payment);
            if (record.CurrencyCode != transaction.CurrencyCode) throw new DomainException("Payment currency must match the transaction currency.");
            transaction.payments.Add(record);
        }

        transaction.Status = status;
        if (status == TransactionStatus.Cancelled) transaction.VoidReason = RequiredReason(voidReason, "Void reason");
        else if (voidReason is not null) throw new DomainException("Only a cancelled transaction can have a void reason.");
        transaction.EnsureRestoredStateIsConsistent();
        return transaction;
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

    private void EnsureRestoredStateIsConsistent()
    {
        var pending = payments.Count(payment => payment.Status == PaymentStatus.Pending);
        var confirmed = payments.Count(payment => payment.Status == PaymentStatus.Confirmed);
        var reversed = payments.Count(payment => payment.Status == PaymentStatus.Reversed);
        var cancelled = payments.Count(payment => payment.Status == PaymentStatus.Cancelled);

        // Declined attempts may appear in any status; each status allows exactly the active payment it implies.
        var consistent = Status switch
        {
            TransactionStatus.Open => pending == 0 && confirmed == 0 && reversed == 0 && cancelled == 0,
            TransactionStatus.PaymentPending => pending == 1 && confirmed == 0 && reversed == 0 && cancelled == 0,
            TransactionStatus.Paid => pending == 0 && confirmed == 1 && reversed == 0 && cancelled == 0,
            TransactionStatus.Reversed => pending == 0 && confirmed == 0 && reversed == 1 && cancelled == 0,
            TransactionStatus.Cancelled => pending == 0 && confirmed == 0 && reversed == 0 && cancelled <= 1,
            _ => false
        };
        if (!consistent)
        {
            throw new DomainException("Persisted payments are inconsistent with the transaction status.");
        }

        // Lines cannot change once payment has started, so the active (or cancelled) attempt must match the basket.
        var active = payments.SingleOrDefault(payment => payment.Status is PaymentStatus.Pending or PaymentStatus.Confirmed or PaymentStatus.Reversed or PaymentStatus.Cancelled);
        if (active is not null && (lineItems.Count == 0 || active.Amount != Total))
        {
            throw new DomainException("Payment amount must equal the transaction total.");
        }
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

    private static string RequiredReason(string? reason, string field)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 250) throw new DomainException($"{field} is required and must not exceed 250 characters.");
        return normalized;
    }

}
