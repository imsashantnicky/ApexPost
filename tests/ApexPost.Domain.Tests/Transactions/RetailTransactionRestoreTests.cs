using ApexPost.Domain.Common;
using ApexPost.Domain.Payments;
using ApexPost.Domain.Transactions;

namespace ApexPost.Domain.Tests.Transactions;

public class RetailTransactionRestoreTests
{
    [Fact]
    public void RestoreKeepsDeclinedAttemptWhenBasketChangedAfterwards()
    {
        var transaction = Restore(TransactionStatus.Open, null,
            [Line(10m), Line(5m)],
            [Payment(10m, PaymentStatus.Declined, declineReason: "Not completed")]);

        Assert.Equal(TransactionStatus.Open, transaction.Status);
        Assert.Equal(15m, transaction.Total);
        Assert.Equal(PaymentStatus.Declined, Assert.Single(transaction.Payments).Status);
    }

    [Fact]
    public void RestoredTransactionContinuesItsLifecycle()
    {
        var payment = Payment(10m, PaymentStatus.Pending);
        var transaction = Restore(TransactionStatus.PaymentPending, null, [Line(10m)], [payment]);

        transaction.ConfirmPayment();

        Assert.Equal(TransactionStatus.Paid, transaction.Status);
        Assert.Equal(payment.Id, transaction.Payments[0].Id);
    }

    [Fact]
    public void RestoreKeepsVoidReasonForCancelledTransaction()
    {
        var transaction = Restore(TransactionStatus.Cancelled, "Customer left", [Line(10m)], [Payment(10m, PaymentStatus.Cancelled)]);

        Assert.Equal("Customer left", transaction.VoidReason);
    }

    public static TheoryData<TransactionStatus, PaymentStatus> StatusMismatches => new()
    {
        { TransactionStatus.Open, PaymentStatus.Pending },
        { TransactionStatus.Open, PaymentStatus.Confirmed },
        { TransactionStatus.PaymentPending, PaymentStatus.Confirmed },
        { TransactionStatus.Paid, PaymentStatus.Pending },
        { TransactionStatus.Paid, PaymentStatus.Reversed },
        { TransactionStatus.Reversed, PaymentStatus.Confirmed },
    };

    [Theory]
    [MemberData(nameof(StatusMismatches))]
    public void RestoreRejectsPaymentsInconsistentWithStatus(TransactionStatus status, PaymentStatus paymentStatus)
    {
        Assert.Throws<DomainException>(() => Restore(status, null, [Line(10m)], [Payment(10m, paymentStatus, reversalReason: "Refund")]));
    }

    [Fact]
    public void RestoreRejectsActivePaymentThatDoesNotMatchTotal()
    {
        Assert.Throws<DomainException>(() => Restore(TransactionStatus.Paid, null, [Line(10m), Line(5m)], [Payment(10m, PaymentStatus.Confirmed)]));
    }

    [Fact]
    public void RestoreRejectsDuplicateLineIdentities()
    {
        var line = Line(10m);
        Assert.Throws<DomainException>(() => Restore(TransactionStatus.Open, null, [line, line], []));
    }

    [Fact]
    public void RestoreRejectsMissingVoidReasonAndStrayVoidReason()
    {
        Assert.Throws<DomainException>(() => Restore(TransactionStatus.Cancelled, null, [Line(10m)], []));
        Assert.Throws<DomainException>(() => Restore(TransactionStatus.Open, "Not cancelled", [Line(10m)], []));
    }

    [Fact]
    public void RestoreRejectsPaymentInAnotherCurrency()
    {
        var payment = Payment(10m, PaymentStatus.Declined, declineReason: "Not completed") with { CurrencyCode = "USD" };
        Assert.Throws<DomainException>(() => Restore(TransactionStatus.Open, null, [Line(10m)], [payment]));
    }

    private static RetailTransaction Restore(TransactionStatus status, string? voidReason, TransactionLineState[] lines, PaymentState[] payments) =>
        RetailTransaction.Restore(EntityId.New(), EntityId.New(), "C-1", "INR", null, status, voidReason, lines, payments);

    private static TransactionLineState Line(decimal unitPrice) => new(EntityId.New(), EntityId.New(), "Counter service", 1, unitPrice);

    private static PaymentState Payment(decimal amount, PaymentStatus status, string? declineReason = null, string? reversalReason = null) =>
        new(EntityId.New(), amount, "INR", PaymentMethod.Cash, "PAY-1", status,
            status == PaymentStatus.Declined ? declineReason ?? "Declined" : null,
            status == PaymentStatus.Reversed ? reversalReason ?? "Refund" : null);
}
