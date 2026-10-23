using ApexPost.Domain.Common;

namespace ApexPost.Domain.Staff;

public sealed class EmployeeNumber : ValueObject
{
    private EmployeeNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static EmployeeNumber From(string value)
    {
        var normalizedValue = value?.Trim().ToUpperInvariant();

        if (normalizedValue is null || !IsValid(normalizedValue))
        {
            throw new DomainException("Employee number must use the format AA-0000, with a two to five letter prefix and a four to eight digit suffix.");
        }

        return new EmployeeNumber(normalizedValue);
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
            && value.Length - separatorIndex - 1 is >= 4 and <= 8
            && value[..separatorIndex].All(char.IsAsciiLetterUpper)
            && value[(separatorIndex + 1)..].All(char.IsAsciiDigit);
    }
}
