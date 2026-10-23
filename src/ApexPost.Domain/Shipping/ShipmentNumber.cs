using ApexPost.Domain.Common;

namespace ApexPost.Domain.Shipping;

public sealed class ShipmentNumber : ValueObject
{
    private ShipmentNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ShipmentNumber From(string value)
    {
        var normalizedValue = value?.Trim().ToUpperInvariant();

        if (normalizedValue is null || !IsValid(normalizedValue))
        {
            throw new DomainException("Shipment number must use the format AA-000000, with a two to five letter prefix and a six to twelve digit suffix.");
        }

        return new ShipmentNumber(normalizedValue);
    }

    public override string ToString()
    {
        return Value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static bool IsValid(string value)
    {
        var separatorIndex = value.IndexOf('-');

        return separatorIndex is >= 2 and <= 5
            && value.Length - separatorIndex - 1 is >= 6 and <= 12
            && value[..separatorIndex].All(char.IsAsciiLetterUpper)
            && value[(separatorIndex + 1)..].All(char.IsAsciiDigit);
    }
}
