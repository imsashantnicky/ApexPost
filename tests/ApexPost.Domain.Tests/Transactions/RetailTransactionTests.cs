using ApexPost.Domain.Common;
using ApexPost.Domain.Payments;
using ApexPost.Domain.Transactions;

namespace ApexPost.Domain.Tests.Transactions;

public class RetailTransactionTests
{
    [Fact]
    public void LineItemsCalculateTransactionTotal()
    {
        var transaction = CreateTransaction();

        transaction.AddLineItem(EntityId.New(), "Express parcel", 2, 125.50m);
        transaction.AddLineItem(EntityId.New(), "Packaging", 1, 20.00m);

        Assert.Equal(271.00m, transaction.Total);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -0.01)]
    public void LineItemWithInvalidQuantityOrPriceThrowsDomainException(int quantity, decimal unitPrice)
    {
        var transaction = CreateTransaction();

        Assert.Throws<DomainException>(() => transaction.AddLineItem(EntityId.New(), "Express parcel", quantity, unitPrice));
    }

    [Fact]
    public void PaymentAmountMustMatchTransactionTotal()
    {
        var transaction = CreateTransactionWithLineItem();

        Assert.Throws<DomainException>(() => transaction.StartPayment(100m, PaymentMethod.Cash, "RCPT-1"));
    }

    [Fact]
    public void PaymentRequiresAtLeastOneLineItem()
    {
        var transaction = CreateTransaction();

        Assert.Throws<DomainException>(() => transaction.StartPayment(1m, PaymentMethod.Cash, "RCPT-1"));
    }

    [Fact]
    public void PendingPaymentCanBeConfirmedAndCompletesTransaction()
    {
        var transaction = CreateTransactionWithLineItem();

        var payment = transaction.StartPayment(125.50m, PaymentMethod.Cash, "RCPT-1");
        transaction.ConfirmPayment();

        Assert.Equal(PaymentStatus.Confirmed, payment.Status);
        Assert.Equal(TransactionStatus.Paid, transaction.Status);
    }

    [Fact]
    public void ConfirmingPaymentTwicePreventsDuplicateProcessing()
    {
        var transaction = CreateTransactionWithLineItem();
        transaction.StartPayment(125.50m, PaymentMethod.Card, "CARD-1");
        transaction.ConfirmPayment();

        Assert.Throws<DomainException>(transaction.ConfirmPayment);
        Assert.Single(transaction.Payments);
    }

    [Fact]
    public void DeclinedPaymentReturnsTransactionToOpenForAnotherAttempt()
    {
        var transaction = CreateTransactionWithLineItem();
        var declinedPayment = transaction.StartPayment(125.50m, PaymentMethod.Card, "CARD-1");

        transaction.DeclinePayment("Issuer declined the payment.");
        var replacementPayment = transaction.StartPayment(125.50m, PaymentMethod.Cash, "RCPT-2");

        Assert.Equal(PaymentStatus.Declined, declinedPayment.Status);
        Assert.Equal(TransactionStatus.PaymentPending, transaction.Status);
        Assert.Equal(PaymentStatus.Pending, replacementPayment.Status);
        Assert.Equal(2, transaction.Payments.Count);
    }

    [Fact]
    public void CancellingPendingTransactionCancelsItsPayment()
    {
        var transaction = CreateTransactionWithLineItem();
        var payment = transaction.StartPayment(125.50m, PaymentMethod.Cash, "RCPT-1");

        transaction.Cancel();

        Assert.Equal(PaymentStatus.Cancelled, payment.Status);
        Assert.Equal(TransactionStatus.Cancelled, transaction.Status);
    }

    [Fact]
    public void PaidTransactionCanBeReversedOnlyOnce()
    {
        var transaction = CreateTransactionWithLineItem();
        var payment = transaction.StartPayment(125.50m, PaymentMethod.Cash, "RCPT-1");
        transaction.ConfirmPayment();

        transaction.ReversePayment();

        Assert.Equal(PaymentStatus.Reversed, payment.Status);
        Assert.Equal(TransactionStatus.Reversed, transaction.Status);
        Assert.Throws<DomainException>(transaction.ReversePayment);
    }

    [Fact]
    public void PaidTransactionCannotBeCancelledOrChanged()
    {
        var transaction = CreateTransactionWithLineItem();
        transaction.StartPayment(125.50m, PaymentMethod.Cash, "RCPT-1");
        transaction.ConfirmPayment();

        Assert.Throws<DomainException>(transaction.Cancel);
        Assert.Throws<DomainException>(() => transaction.AddLineItem(EntityId.New(), "Packaging", 1, 20m));
    }

    [Fact]
    public void PaymentWithInvalidDecimalAmountIsRejected()
    {
        var transaction = CreateTransactionWithLineItem();

        Assert.Throws<DomainException>(() => transaction.StartPayment(125.505m, PaymentMethod.Cash, "RCPT-1"));
    }

    private static RetailTransaction CreateTransaction()
    {
        return new RetailTransaction(EntityId.New(), EntityId.New(), "Counter 1", "INR");
    }

    private static RetailTransaction CreateTransactionWithLineItem()
    {
        var transaction = CreateTransaction();
        transaction.AddLineItem(EntityId.New(), "Express parcel", 1, 125.50m);
        return transaction;
    }
}
