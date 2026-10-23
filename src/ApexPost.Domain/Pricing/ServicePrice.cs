using ApexPost.Domain.Common;

namespace ApexPost.Domain.Pricing;

public sealed class ServicePrice : ValueObject
{
    private ServicePrice(decimal amount, string currencyCode)
    {
        Amount = amount;
        CurrencyCode = currencyCode;
    }

    public decimal Amount { get; }

    public string CurrencyCode { get; }

    public static ServicePrice From(decimal amount, string currencyCode)
    {
        if (amount <= 0)
        {
            throw new DomainException("Service price must be greater than zero.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new DomainException("Service price cannot have more than two decimal places.");
        }

        return new ServicePrice(amount, ApexPost.Domain.Common.CurrencyCode.From(currencyCode).Value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return CurrencyCode;
    }
}
