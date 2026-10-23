using ApexPost.Domain.Common;

namespace ApexPost.Domain.Shipping;

public sealed class ShipmentCharge : ValueObject
{
    public ShipmentCharge(decimal amount, string currencyCode)
    {
        if (amount <= 0 || decimal.Round(amount, 2) != amount)
        {
            throw new DomainException("Shipment charge must be greater than zero and have no more than two decimal places.");
        }

        Amount = amount;
        CurrencyCode = ApexPost.Domain.Common.CurrencyCode.From(currencyCode).Value;
    }

    public decimal Amount { get; }

    public string CurrencyCode { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return CurrencyCode;
    }
}
