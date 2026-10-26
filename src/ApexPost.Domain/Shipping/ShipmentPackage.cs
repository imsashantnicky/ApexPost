using ApexPost.Domain.Common;

namespace ApexPost.Domain.Shipping;

public sealed class ShipmentPackage : ValueObject
{
    public const decimal MaximumWeightKilograms = 70m;

    public ShipmentPackage(decimal weightKilograms, string contentsDescription, decimal maximumWeightKilograms = MaximumWeightKilograms)
    {
        if (maximumWeightKilograms <= 0) throw new DomainException("Maximum package weight must be greater than zero.");
        if (weightKilograms <= 0 || weightKilograms > maximumWeightKilograms || decimal.Round(weightKilograms, 3) != weightKilograms)
        {
            throw new DomainException($"Package weight must be greater than zero, no more than {maximumWeightKilograms} kilograms, and have no more than three decimal places.");
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
