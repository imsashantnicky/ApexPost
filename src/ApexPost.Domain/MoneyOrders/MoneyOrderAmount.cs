using ApexPost.Domain.Common;

namespace ApexPost.Domain.MoneyOrders;

public sealed class MoneyOrderAmount : ValueObject
{
    public MoneyOrderAmount(decimal amount, string currencyCode)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
        {
            throw new DomainException("Money order amount must be greater than zero and have no more than two decimal places.");
        }

        Amount = amount;
        CurrencyCode = ApexPost.Domain.Common.CurrencyCode.From(currencyCode).Value;
    }

    public decimal Amount { get; private set => field = value; }

    public string CurrencyCode { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return CurrencyCode;
    }
}
