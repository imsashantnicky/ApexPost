namespace ApexPost.Domain.Transactions;

public enum TransactionStatus
{
    Open,
    PaymentPending,
    Paid,
    Cancelled,
    Reversed
}
