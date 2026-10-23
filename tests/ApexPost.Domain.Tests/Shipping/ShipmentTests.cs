using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;

namespace ApexPost.Domain.Tests.Shipping;

public class ShipmentTests
{
    [Fact]
    public void ConstructorWithValidValuesCreatesBookedShipment()
    {
        var shipment = CreateShipment();

        Assert.Equal("SHP-000001", shipment.ShipmentNumber.Value);
        Assert.Equal(ShipmentStatus.Booked, shipment.Status);
        Assert.Equal(1.250m, shipment.Package.WeightKilograms);
        Assert.Equal(125.50m, shipment.Charge.Amount);
    }

    [Fact]
    public void ShipmentNumberWithSameValueIsEqualAfterNormalization()
    {
        var first = ShipmentNumber.From("shp-000001");
        var second = ShipmentNumber.From(" SHP-000001 ");

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SHP-0001")]
    [InlineData("S-000001")]
    [InlineData("SHP-0000A1")]
    public void ShipmentNumberWithInvalidFormatThrowsDomainException(string value)
    {
        Assert.Throws<DomainException>(() => ShipmentNumber.From(value));
    }

    [Fact]
    public void PackageWithInvalidWeightThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new ShipmentPackage(0m, "Documents"));
        Assert.Throws<DomainException>(() => new ShipmentPackage(1.2345m, "Documents"));
    }

    [Fact]
    public void ChargeWithInvalidAmountThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new ShipmentCharge(-1m, "INR"));
        Assert.Throws<DomainException>(() => new ShipmentCharge(12.345m, "INR"));
    }

    [Fact]
    public void ShipmentFollowsBookedAcceptedInTransitDeliveredLifecycle()
    {
        var shipment = CreateShipment();

        shipment.Accept();
        shipment.Dispatch();
        shipment.Deliver();

        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
    }

    [Fact]
    public void ShipmentCannotSkipAStatusTransition()
    {
        var shipment = CreateShipment();

        Assert.Throws<DomainException>(shipment.Dispatch);
        Assert.Throws<DomainException>(shipment.Deliver);
    }

    [Fact]
    public void AcceptedShipmentCanBeCancelledButCannotBeDispatchedAfterward()
    {
        var shipment = CreateShipment();
        shipment.Accept();
        shipment.Cancel();

        Assert.Equal(ShipmentStatus.Cancelled, shipment.Status);
        Assert.Throws<DomainException>(shipment.Dispatch);
    }

    [Fact]
    public void InTransitShipmentCannotBeCancelled()
    {
        var shipment = CreateShipment();
        shipment.Accept();
        shipment.Dispatch();

        Assert.Throws<DomainException>(shipment.Cancel);
    }

    [Fact]
    public void ShipmentPartyWithMissingNameThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new ShipmentParty(" ", CreateAddress()));
    }

    private static Shipment CreateShipment()
    {
        return new Shipment(
            EntityId.New(),
            ShipmentNumber.From("SHP-000001"),
            new ShipmentParty("Asha Rao", CreateAddress()),
            new ShipmentParty("Rahul Verma", new ShipmentAddress("2 Lake Road", "Mysuru", "570001", "IN")),
            EntityId.New(),
            new ShipmentPackage(1.250m, "Documents"),
            new ShipmentCharge(125.50m, "INR"));
    }

    private static ShipmentAddress CreateAddress()
    {
        return new ShipmentAddress("1 Market Road", "Bengaluru", "560001", "IN");
    }
}
