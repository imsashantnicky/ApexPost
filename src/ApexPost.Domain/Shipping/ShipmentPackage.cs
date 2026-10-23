using ApexPost.Domain.Common;

namespace ApexPost.Domain.Shipping;

public sealed class ShipmentPackage : ValueObject
{
    public ShipmentPackage(decimal weightKilograms, string contentsDescription)
    {
        if (weightKilograms <= 0 || decimal.Round(weightKilograms, 3) != weightKilograms)
        {
            throw new DomainException("Package weight must be greater than zero and have no more than three decimal places.");
        }

        var normalizedDescription = contentsDescription?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedDescription) || normalizedDescription.Length > 500)
        {
            throw new DomainException("Package contents description is required and must not exceed 500 characters.");
        }

        WeightKilograms = weightKilograms;
        ContentsDescription = normalizedDescription;
    }

    public decimal WeightKilograms { get; }

    public string ContentsDescription { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return WeightKilograms;
        yield return ContentsDescription;
    }
}
