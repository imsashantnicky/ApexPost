using ApexPost.Domain.Common;

namespace ApexPost.Domain.Branches;

public sealed class BranchAddress : ValueObject
{
    public BranchAddress(string line1, string? line2, string city, string postalCode, string countryCode)
    {
        Line1 = RequiredText(line1, "Address line 1", 100);
        Line2 = OptionalText(line2, "Address line 2", 100);
        City = RequiredText(city, "City", 80);
        PostalCode = PostalCodeText(postalCode);
        CountryCode = CountryCodeText(countryCode);
    }

    public string Line1 { get; }

    public string? Line2 { get; }

    public string City { get; }

    public string PostalCode { get; }

    public string CountryCode { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Line1;
        yield return Line2;
        yield return City;
        yield return PostalCode;
        yield return CountryCode;
    }

    private static string RequiredText(string value, string fieldName, int maximumLength)
    {
        var normalizedValue = value?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedValue) || normalizedValue.Length > maximumLength)
        {
            throw new DomainException($"{fieldName} is required and must not exceed {maximumLength} characters.");
        }

        return normalizedValue;
    }

    private static string? OptionalText(string? value, string fieldName, int maximumLength)
    {
        var normalizedValue = value?.Trim();

        if (string.IsNullOrEmpty(normalizedValue))
        {
            return null;
        }

        if (normalizedValue.Length > maximumLength)
        {
            throw new DomainException($"{fieldName} must not exceed {maximumLength} characters.");
        }

        return normalizedValue;
    }

    private static string PostalCodeText(string value)
    {
        var normalizedValue = value?.Trim().ToUpperInvariant();

        if (normalizedValue is null
            || normalizedValue.Length is < 3 or > 12
            || !normalizedValue.All(character => char.IsAsciiLetterOrDigit(character) || character is ' ' or '-'))
        {
            throw new DomainException("Postal code must contain three to twelve letters, digits, spaces, or hyphens.");
        }

        return normalizedValue;
    }

    private static string CountryCodeText(string value)
    {
        var normalizedValue = value?.Trim().ToUpperInvariant();

        if (normalizedValue is null || normalizedValue.Length != 2 || !normalizedValue.All(char.IsAsciiLetterUpper))
        {
            throw new DomainException("Country code must be a two-letter ISO-style code.");
        }

        return normalizedValue;
    }
}
