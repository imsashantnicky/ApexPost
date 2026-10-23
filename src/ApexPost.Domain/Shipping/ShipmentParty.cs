using ApexPost.Domain.Common;

namespace ApexPost.Domain.Shipping;

public sealed class ShipmentParty : ValueObject
{
    public ShipmentParty(string name, ShipmentAddress address)
    {
        var normalizedName = name?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 150)
        {
            throw new DomainException("Shipment party name is required and must not exceed 150 characters.");
        }

        Name = normalizedName;
        Address = address ?? throw new DomainException("Shipment party address is required.");
    }

    public string Name { get; }

    public ShipmentAddress Address { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Address;
    }
}
