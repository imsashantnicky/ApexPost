using ApexPost.Domain.Common;
using ApexPost.Domain.Pricing;

namespace ApexPost.Domain.Tests.Pricing;

public class PricingRuleTests
{
    [Fact]
    public void ServicePriceWithValidAmountAndCurrencyCreatesPrice()
    {
        var price = ServicePrice.From(125.50m, "inr");

        Assert.Equal(125.50m, price.Amount);
        Assert.Equal("INR", price.CurrencyCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    public void ServicePriceWithNonPositiveAmountThrowsDomainException(decimal amount)
    {
        Assert.Throws<DomainException>(() => ServicePrice.From(amount, "INR"));
    }

    [Fact]
    public void ServicePriceWithMoreThanTwoDecimalPlacesThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => ServicePrice.From(12.345m, "INR"));
    }

    [Fact]
    public void ServicePriceWithInvalidCurrencyCodeThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => ServicePrice.From(12.00m, "RUPEE"));
    }

    [Fact]
    public void PricingRuleIsEffectiveWithinInclusiveDateRange()
    {
        var rule = CreateRule(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.False(rule.IsEffectiveOn(new DateOnly(2025, 12, 31)));
        Assert.True(rule.IsEffectiveOn(new DateOnly(2026, 1, 1)));
        Assert.True(rule.IsEffectiveOn(new DateOnly(2026, 1, 31)));
        Assert.False(rule.IsEffectiveOn(new DateOnly(2026, 2, 1)));
    }

    [Fact]
    public void PricingRuleWithEndDateBeforeStartDateThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => CreateRule(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 31)));
    }

    [Fact]
    public void ChangePriceUpdatesTheControlledPriceValue()
    {
        var rule = CreateRule(new DateOnly(2026, 1, 1), null);

        rule.ChangePrice(ServicePrice.From(150.00m, "INR"));

        Assert.Equal(150.00m, rule.Price.Amount);
    }

    private static PricingRule CreateRule(DateOnly effectiveFrom, DateOnly? effectiveTo)
    {
        return new PricingRule(
            EntityId.New(),
            EntityId.New(),
            ServicePrice.From(125.50m, "INR"),
            effectiveFrom,
            effectiveTo);
    }
}
