using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.IdentityVerification;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.MoneyOrders;
using ApexPost.Domain.Payments;
using ApexPost.Domain.Pricing;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Staff;
using ApexPost.Domain.Transactions;

namespace ApexPost.Domain.Tests;

public class DomainRuleReviewTests
{
    [Fact]
    public void ClosedBranchRejectsRenameAndAddressChange()
    {
        var branch = new Branch(EntityId.New(), BranchCode.From("BLR-001"), "Central", BranchAddress(), 1);
        branch.Close();

        Assert.Throws<DomainException>(() => branch.Rename("New Central"));
        Assert.Throws<DomainException>(() => branch.ChangeAddress(BranchAddress()));
    }

    [Fact]
    public void UnavailableServiceCannotBeMadeUnavailableAgain()
    {
        var service = new ServiceDefinition(EntityId.New(), ServiceCode.From("EXP-001"), "Express", "Express delivery.");
        service.MakeUnavailable();

        Assert.Throws<DomainException>(service.MakeUnavailable);
    }

    [Fact]
    public void InvalidPricingPeriodDoesNotAlterExistingPeriod()
    {
        var rule = new PricingRule(
            EntityId.New(),
            EntityId.New(),
            ServicePrice.From(10m, "INR"),
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31));

        Assert.Throws<DomainException>(() => rule.ChangeEffectivePeriod(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 31)));

        Assert.Equal(new DateOnly(2026, 1, 1), rule.EffectiveFrom);
        Assert.Equal(new DateOnly(2026, 1, 31), rule.EffectiveTo);
    }

    [Fact]
    public void InventoryRejectsDefaultTimestampAndPreservesQuantity()
    {
        var item = InventoryItem();

        Assert.Throws<DomainException>(() => item.Receive(1, "Delivery", default));

        Assert.Equal(0, item.QuantityOnHand);
        Assert.Empty(item.Movements);
    }

    [Fact]
    public void InventoryRejectsOverflowAndPreservesQuantity()
    {
        var item = InventoryItem();
        item.Receive(int.MaxValue, "Initial count", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => item.Receive(1, "Additional count", DateTimeOffset.UtcNow));

        Assert.Equal(int.MaxValue, item.QuantityOnHand);
        Assert.Single(item.Movements);
    }

    [Fact]
    public void StaffRejectsUnrecognizedRole()
    {
        Assert.Throws<DomainException>(() => new StaffMember(
            EntityId.New(),
            EmployeeNumber.From("EMP-0001"),
            "Asha Rao",
            EntityId.New(),
            [(StaffRole)999]));
    }

    [Fact]
    public void VerificationRejectsDefaultCompletionTime()
    {
        var verification = new IdentityVerificationRecord(EntityId.New(), EntityId.New(), VerificationMethod.InPersonReview);

        Assert.Throws<DomainException>(() => verification.MarkVerified(default));
        Assert.Equal(VerificationStatus.Pending, verification.Status);
    }

    [Fact]
    public void DeliveredShipmentCannotBeCancelledOrDeliveredTwice()
    {
        var shipment = Shipment();
        shipment.Accept();
        shipment.Dispatch();
        shipment.Deliver();

        Assert.Throws<DomainException>(shipment.Cancel);
        Assert.Throws<DomainException>(shipment.Deliver);
    }

    [Fact]
    public void PaidMoneyOrderCannotBeCancelledOrExpired()
    {
        var order = new MoneyOrder(
            EntityId.New(),
            MoneyOrderNumber.From("MO-000001"),
            new MoneyOrderAmount(10m, "INR"),
            EntityId.New(),
            new MoneyOrderBeneficiary("Asha Rao", "BEN-1"));
        order.MarkPaid();

        Assert.Throws<DomainException>(order.Cancel);
        Assert.Throws<DomainException>(order.Expire);
    }

    [Fact]
    public void TransactionRejectsUnknownPaymentMethodAndRemainsOpen()
    {
        var transaction = new RetailTransaction(EntityId.New(), EntityId.New(), "Counter 1", "INR");
        transaction.AddLineItem(EntityId.New(), "Express", 1, 10m);

        Assert.Throws<DomainException>(() => transaction.StartPayment(10m, (PaymentMethod)999, "RCPT-1"));

        Assert.Equal(TransactionStatus.Open, transaction.Status);
        Assert.Empty(transaction.Payments);
    }

    [Fact]
    public void TransactionLineCanBeRemovedOnlyWhileOpen()
    {
        var transaction = new RetailTransaction(EntityId.New(), EntityId.New(), "Counter 1", "INR");
        var lineItem = transaction.AddLineItem(EntityId.New(), "Express", 1, 10m);

        transaction.RemoveLineItem(lineItem.Id);

        Assert.Equal(0m, transaction.Total);
        Assert.Empty(transaction.LineItems);
    }

    private static BranchAddress BranchAddress()
    {
        return new BranchAddress("1 Market Road", null, "Bengaluru", "560001", "IN");
    }

    private static InventoryItem InventoryItem()
    {
        return new InventoryItem(EntityId.New(), EntityId.New(), InventorySku.From("ENV-001"), "Envelope", 1);
    }

    private static Shipment Shipment()
    {
        var address = new ShipmentAddress("1 Market Road", "Bengaluru", "560001", "IN");
        return new Shipment(
            EntityId.New(),
            ShipmentNumber.From("SHP-000001"),
            new ShipmentParty("Asha Rao", address),
            new ShipmentParty("Rahul Verma", address),
            EntityId.New(),
            new ShipmentPackage(1m, "Documents"),
            new ShipmentCharge(10m, "INR"));
    }
}
