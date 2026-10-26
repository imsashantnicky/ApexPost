using ApexPost.Domain.Common;

namespace ApexPost.Domain.Shipping;

public sealed class Shipment
{
    public Shipment(
        EntityId id,
        ShipmentNumber shipmentNumber,
        ShipmentParty sender,
        ShipmentParty recipient,
        EntityId serviceId,
        ShipmentPackage package,
        ShipmentCharge charge)
        : this(id, shipmentNumber, sender, recipient, serviceId, package, charge, null)
    {
    }

    public Shipment(
        EntityId id,
        ShipmentNumber shipmentNumber,
        ShipmentParty sender,
        ShipmentParty recipient,
        EntityId serviceId,
        ShipmentPackage package,
        ShipmentCharge charge,
        EntityId? branchId,
        string zoneCode = "UNKNOWN",
        ShippingServiceLevel serviceLevel = ShippingServiceLevel.Standard)
    {
        Id = id ?? throw new DomainException("Shipment identity is required.");
        ShipmentNumber = shipmentNumber ?? throw new DomainException("Shipment number is required.");
        Sender = sender ?? throw new DomainException("Shipment sender is required.");
        Recipient = recipient ?? throw new DomainException("Shipment recipient is required.");
        ServiceId = serviceId ?? throw new DomainException("Service identity is required.");
        Package = package ?? throw new DomainException("Shipment package is required.");
        Charge = charge ?? throw new DomainException("Shipment charge is required.");
        BranchId = branchId;
        ZoneCode = RequiredZoneCode(zoneCode);
        if (!Enum.IsDefined(serviceLevel)) throw new DomainException("Shipping service level must be recognized.");
        ServiceLevel = serviceLevel;
        Status = ShipmentStatus.Booked;
    }

    public EntityId Id { get; }

    public ShipmentNumber ShipmentNumber { get; }

    public ShipmentParty Sender { get; }

    public ShipmentParty Recipient { get; }

    public EntityId ServiceId { get; }

    public ShipmentPackage Package { get; }

    public ShipmentCharge Charge { get; }

    public EntityId? BranchId { get; }

    public string ZoneCode { get; }

    public ShippingServiceLevel ServiceLevel { get; }

    public ShipmentStatus Status { get; private set; }

    public void Accept()
    {
        ChangeStatus(ShipmentStatus.Booked, ShipmentStatus.Accepted, "Only a booked shipment can be accepted.");
    }

    public void Dispatch()
    {
        ChangeStatus(ShipmentStatus.Accepted, ShipmentStatus.InTransit, "Only an accepted shipment can be dispatched.");
    }

    public void Deliver()
    {
        ChangeStatus(ShipmentStatus.InTransit, ShipmentStatus.Delivered, "Only an in-transit shipment can be delivered.");
    }

    public void Cancel()
    {
        if (Status is not (ShipmentStatus.Booked or ShipmentStatus.Accepted))
        {
            throw new DomainException("Only a booked or accepted shipment can be cancelled.");
        }

        Status = ShipmentStatus.Cancelled;
    }

    public static Shipment Restore(EntityId id, ShipmentNumber shipmentNumber, ShipmentParty sender, ShipmentParty recipient, EntityId serviceId, ShipmentPackage package, ShipmentCharge charge, ShipmentStatus status)
        => Restore(id, shipmentNumber, sender, recipient, serviceId, package, charge, status, null);

    public static Shipment Restore(EntityId id, ShipmentNumber shipmentNumber, ShipmentParty sender, ShipmentParty recipient, EntityId serviceId, ShipmentPackage package, ShipmentCharge charge, ShipmentStatus status, EntityId? branchId, string zoneCode = "UNKNOWN", ShippingServiceLevel serviceLevel = ShippingServiceLevel.Standard)
    {
        var shipment = new Shipment(id, shipmentNumber, sender, recipient, serviceId, package, charge, branchId, zoneCode, serviceLevel);
        if (status == ShipmentStatus.Accepted) shipment.Accept();
        else if (status == ShipmentStatus.InTransit) { shipment.Accept(); shipment.Dispatch(); }
        else if (status == ShipmentStatus.Delivered) { shipment.Accept(); shipment.Dispatch(); shipment.Deliver(); }
        else if (status == ShipmentStatus.Cancelled) shipment.Cancel();
        else if (status != ShipmentStatus.Booked) throw new DomainException("Shipment status must be recognized.");
        return shipment;
    }

    private void ChangeStatus(ShipmentStatus expectedStatus, ShipmentStatus targetStatus, string errorMessage)
    {
        if (Status != expectedStatus)
        {
            throw new DomainException(errorMessage);
        }

        Status = targetStatus;
    }

    private static string RequiredZoneCode(string zoneCode)
    {
        var normalized = zoneCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 20 || normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            throw new DomainException("Shipping zone code must contain only letters, digits, or hyphens and be no longer than 20 characters.");
        return normalized;
    }
}
