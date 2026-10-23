using ApexPost.Domain.Common;

namespace ApexPost.Domain.Tests.Common;

public class CurrencyCodeTests
{
    [Fact]
    public void FromNormalizesCaseAndProducesEqualCodes()
    {
        var first = CurrencyCode.From("inr");
        var second = CurrencyCode.From(" INR ");

        Assert.Equal("INR", first.Value);
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("IN")]
    [InlineData("INRR")]
    [InlineData("I1R")]
    public void FromWithInvalidValueThrowsDomainException(string value)
    {
        Assert.Throws<DomainException>(() => CurrencyCode.From(value));
    }
}
