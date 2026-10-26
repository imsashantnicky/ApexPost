using ApexPost.Domain.Shipping;

namespace ApexPost.Application.Shipping;

public sealed record BookShipmentRequest(Guid BranchId, string ShipmentNumber, string SenderName, string SenderAddressLine1, string SenderCity, string SenderPostalCode, string SenderCountryCode, string RecipientName, string RecipientAddressLine1, string RecipientCity, string RecipientPostalCode, string RecipientCountryCode, Guid ServiceId, decimal WeightKilograms, string ContentsDescription, decimal ChargeAmount, string CurrencyCode)
{
    public string ZoneCode { get; init; } = "UNKNOWN";
    public ShippingServiceLevel ServiceLevel { get; init; } = ShippingServiceLevel.Standard;
    public BookShipmentRequest(string shipmentNumber, string senderName, string senderAddressLine1, string senderCity, string senderPostalCode, string senderCountryCode, string recipientName, string recipientAddressLine1, string recipientCity, string recipientPostalCode, string recipientCountryCode, Guid serviceId, decimal weightKilograms, string contentsDescription, decimal chargeAmount, string currencyCode)
        : this(Guid.Empty, shipmentNumber, senderName, senderAddressLine1, senderCity, senderPostalCode, senderCountryCode, recipientName, recipientAddressLine1, recipientCity, recipientPostalCode, recipientCountryCode, serviceId, weightKilograms, contentsDescription, chargeAmount, currencyCode) { }
}
public sealed record ShipmentResponse(Guid Id, long Version, Guid BranchId, string ShipmentNumber, ShipmentStatus Status, decimal ChargeAmount, string CurrencyCode)
{
    public string ZoneCode { get; init; } = "UNKNOWN";
    public ShippingServiceLevel ServiceLevel { get; init; }
}
