using ApexPost.Domain.Common;

namespace ApexPost.Domain.Inventory;

public sealed class InventorySku : ValueObject
{
    private InventorySku(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static InventorySku From(string value)
    {
        var normalizedValue = value?.Trim().ToUpperInvariant();

        if (normalizedValue is null || !IsValid(normalizedValue))
        {
            throw new DomainException("Inventory SKU must use the format AA-000, with a two to five letter prefix and a three to six digit suffix.");
        }

        return new InventorySku(normalizedValue);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    private static bool IsValid(string value)
    {
        var separatorIndex = value.IndexOf('-');

        return separatorIndex is >= 2 and <= 5
            && value.Length - separatorIndex - 1 is >= 3 and <= 6
            && value[..separatorIndex].All(char.IsAsciiLetterUpper)
            && value[(separatorIndex + 1)..].All(char.IsAsciiDigit);
    }
}
