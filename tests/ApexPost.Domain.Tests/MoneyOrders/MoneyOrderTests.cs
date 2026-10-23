using ApexPost.Domain.Common;
using ApexPost.Domain.MoneyOrders;

namespace ApexPost.Domain.Tests.MoneyOrders;

public class MoneyOrderTests
{
    [Fact]
    public void ConstructorWithValidValuesCreatesIssuedMoneyOrder()
    {
        var moneyOrder = CreateMoneyOrder();

        Assert.Equal("MO-000001", moneyOrder.OrderNumber.Value);
        Assert.Equal(250.00m, moneyOrder.Amount.Amount);
        Assert.Equal("INR", moneyOrder.Amount.CurrencyCode);
        Assert.Equal(MoneyOrderStatus.Issued, moneyOrder.Status);
    }

    [Fact]
    public void MoneyOrderNumberWithSameValueIsEqualAfterNormalization()
    {
        var first = MoneyOrderNumber.From("mo-000001");
        var second = MoneyOrderNumber.From(" MO-000001 ");

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("MO-001")]
    [InlineData("M-000001")]
    [InlineData("MO-00000A")]
    public void MoneyOrderNumberWithInvalidFormatThrowsDomainException(string value)
    {
        Assert.Throws<DomainException>(() => MoneyOrderNumber.From(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MoneyOrderAmountWithNonPositiveValueThrowsDomainException(decimal amount)
    {
        Assert.Throws<DomainException>(() => new MoneyOrderAmount(amount, "INR"));
    }

    [Fact]
    public void MoneyOrderAmountWithMoreThanTwoDecimalPlacesThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new MoneyOrderAmount(10.999m, "INR"));
    }

    [Fact]
    public void BeneficiaryWithMissingReferenceThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new MoneyOrderBeneficiary("Anika Sharma", " "));
    }

    [Fact]
    public void IssuedMoneyOrderCanBeMarkedPaid()
    {
        var moneyOrder = CreateMoneyOrder();

        moneyOrder.MarkPaid();

        Assert.Equal(MoneyOrderStatus.Paid, moneyOrder.Status);
    }

    [Fact]
    public void CancelledMoneyOrderCannotBePaidOrExpired()
    {
        var moneyOrder = CreateMoneyOrder();
        moneyOrder.Cancel();

        Assert.Equal(MoneyOrderStatus.Cancelled, moneyOrder.Status);
        Assert.Throws<DomainException>(moneyOrder.MarkPaid);
        Assert.Throws<DomainException>(moneyOrder.Expire);
    }

    [Fact]
    public void IssuedMoneyOrderCanExpire()
    {
        var moneyOrder = CreateMoneyOrder();

        moneyOrder.Expire();

        Assert.Equal(MoneyOrderStatus.Expired, moneyOrder.Status);
    }

    private static MoneyOrder CreateMoneyOrder()
    {
        return new MoneyOrder(
            EntityId.New(),
            MoneyOrderNumber.From("MO-000001"),
            new MoneyOrderAmount(250.00m, "INR"),
            EntityId.New(),
            new MoneyOrderBeneficiary("Anika Sharma", "BEN-1001"));
    }
}
