using ApexPost.Domain.Common;

namespace ApexPost.Domain.Branches;

public sealed class BranchCode : ValueObject
{
    private BranchCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static BranchCode From(string value)
    {
        var normalizedValue = value?.Trim().ToUpperInvariant();

        if (normalizedValue is null || !IsValid(normalizedValue))
        {
            throw new DomainException("A branch code must use the format AA-000, with a two to five letter prefix and a three to six digit suffix.");
        }

        return new BranchCode(normalizedValue);
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
            && value.Length - separatorIndex - 1 is >= 3 and <= 6
            && value[..separatorIndex].All(char.IsAsciiLetterUpper)
            && value[(separatorIndex + 1)..].All(char.IsAsciiDigit);
    }
}
