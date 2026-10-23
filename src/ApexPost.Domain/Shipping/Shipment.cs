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
    {
        Id = id ?? throw new DomainException("Shipment identity is required.");
        ShipmentNumber = shipmentNumber ?? throw new DomainException("Shipment number is required.");
        Sender = sender ?? throw new DomainException("Shipment sender is required.");
        Recipient = recipient ?? throw new DomainException("Shipment recipient is required.");
        ServiceId = serviceId ?? throw new DomainException("Service identity is required.");
        Package = package ?? throw new DomainException("Shipment package is required.");
        Charge = charge ?? throw new DomainException("Shipment charge is required.");
        Status = ShipmentStatus.Booked;
    }

    public EntityId Id { get; }

    public ShipmentNumber ShipmentNumber { get; }

    public ShipmentParty Sender { get; }

    public ShipmentParty Recipient { get; }

    public EntityId ServiceId { get; }

    public ShipmentPackage Package { get; }

    public ShipmentCharge Charge { get; }

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

    private void ChangeStatus(ShipmentStatus expectedStatus, ShipmentStatus targetStatus, string errorMessage)
    {
        if (Status != expectedStatus)
        {
            throw new DomainException(errorMessage);
        }

        Status = targetStatus;
    }
}
