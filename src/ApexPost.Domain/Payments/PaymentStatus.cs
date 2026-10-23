namespace ApexPost.Domain.Payments;

public enum PaymentStatus
{
    Pending,
    Confirmed,
    Declined,
    Cancelled,
    Reversed
}
