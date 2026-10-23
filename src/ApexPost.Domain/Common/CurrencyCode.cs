namespace ApexPost.Domain.Common;

/// <summary>
/// A normalized three-letter ISO-style currency code.
/// </summary>
public sealed class CurrencyCode : ValueObject
{
    private CurrencyCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static CurrencyCode From(string value)
    {
        var normalizedValue = value?.Trim().ToUpperInvariant();

        if (normalizedValue is null
            || normalizedValue.Length != 3
            || !normalizedValue.All(char.IsAsciiLetterUpper))
        {
            throw new DomainException("Currency code must be a three-letter ISO-style code.");
        }

        return new CurrencyCode(normalizedValue);
    }

    public override string ToString()
    {
        return Value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
