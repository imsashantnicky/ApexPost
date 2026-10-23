using ApexPost.Domain.Shipping;

namespace ApexPost.Application.Shipping;

public sealed record BookShipmentRequest(string ShipmentNumber, string SenderName, string SenderAddressLine1, string SenderCity, string SenderPostalCode, string SenderCountryCode, string RecipientName, string RecipientAddressLine1, string RecipientCity, string RecipientPostalCode, string RecipientCountryCode, Guid ServiceId, decimal WeightKilograms, string ContentsDescription, decimal ChargeAmount, string CurrencyCode);
public sealed record ShipmentResponse(Guid Id, long Version, string ShipmentNumber, ShipmentStatus Status, decimal ChargeAmount, string CurrencyCode);
