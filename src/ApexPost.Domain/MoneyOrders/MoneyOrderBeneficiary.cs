using ApexPost.Domain.Common;

namespace ApexPost.Domain.MoneyOrders;

public sealed class MoneyOrderBeneficiary : ValueObject
{
    public MoneyOrderBeneficiary(string name, string reference)
    {
        var normalizedName = name?.Trim();
        var normalizedReference = reference?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 150)
        {
            throw new DomainException("Beneficiary name is required and must not exceed 150 characters.");
        }

        if (string.IsNullOrWhiteSpace(normalizedReference) || normalizedReference.Length > 100)
        {
            throw new DomainException("Beneficiary reference is required and must not exceed 100 characters.");
        }

        Name = normalizedName;
        Reference = normalizedReference;
    }

    public string Name { get; }

    public string Reference { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Reference;
    }
}
