using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;

namespace ApexPost.Domain.Tests.Inventory;

public class InventoryItemTests
{
    [Fact]
    public void ConstructorCreatesEmptyInventoryAtItsReorderLevel()
    {
        var item = CreateItem(reorderLevel: 5);

        Assert.Equal(0, item.QuantityOnHand);
        Assert.True(item.NeedsReorder);
        Assert.Empty(item.Movements);
    }

    [Fact]
    public void ConstructorWithNegativeReorderLevelThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => CreateItem(reorderLevel: -1));
    }

    [Fact]
    public void RenameNormalizesNameAndRejectsInvalidValuesWithoutChangingCurrentName()
    {
        var item = CreateItem();
        item.Rename("  Mailing box  ");

        Assert.Equal("Mailing box", item.Name);
        Assert.Throws<DomainException>(() => item.Rename("   "));
        Assert.Equal("Mailing box", item.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReceiveWithNonPositiveQuantityThrowsDomainException(int quantity)
    {
        var item = CreateItem();

        Assert.Throws<DomainException>(() => item.Receive(quantity, "Supplier delivery", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ReceiveCreatesReceiptMovementAndIncreasesStock()
    {
        var item = CreateItem();

        item.Receive(10, "Supplier delivery", new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(10, item.QuantityOnHand);
        var movement = Assert.Single(item.Movements);
        Assert.Equal(StockMovementType.Receipt, movement.Type);
        Assert.Equal(10, movement.QuantityChange);
    }

    [Fact]
    public void IssueWithinAvailableStockCreatesIssueMovement()
    {
        var item = CreateItem();
        item.Receive(10, "Supplier delivery", DateTimeOffset.UtcNow);

        item.Issue(4, "Counter sale", DateTimeOffset.UtcNow);

        Assert.Equal(6, item.QuantityOnHand);
        var movement = item.Movements[^1];
        Assert.Equal(StockMovementType.Issue, movement.Type);
        Assert.Equal(-4, movement.QuantityChange);
    }

    [Fact]
    public void IssueBeyondAvailableStockThrowsAndPreservesQuantity()
    {
        var item = CreateItem();
        item.Receive(3, "Supplier delivery", DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => item.Issue(4, "Counter sale", DateTimeOffset.UtcNow));

        Assert.Equal(3, item.QuantityOnHand);
        Assert.Single(item.Movements);
    }

    [Fact]
    public void AdjustmentCreatesDecreaseMovementWithoutNegativeStock()
    {
        var item = CreateItem();
        item.Receive(10, "Supplier delivery", DateTimeOffset.UtcNow);

        item.AdjustTo(7, "Stock count", DateTimeOffset.UtcNow);

        Assert.Equal(7, item.QuantityOnHand);
        var movement = item.Movements[^1];
        Assert.Equal(StockMovementType.AdjustmentDecrease, movement.Type);
        Assert.Equal(-3, movement.QuantityChange);
    }

    [Fact]
    public void AdjustmentToNegativeQuantityThrowsDomainException()
    {
        var item = CreateItem();

        Assert.Throws<DomainException>(() => item.AdjustTo(-1, "Stock count", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AdjustmentWithoutQuantityChangeThrowsDomainException()
    {
        var item = CreateItem();

        Assert.Throws<DomainException>(() => item.AdjustTo(0, "Stock count", DateTimeOffset.UtcNow));
    }

    private static InventoryItem CreateItem(int reorderLevel = 2)
    {
        return new InventoryItem(
            EntityId.New(),
            EntityId.New(),
            InventorySku.From("ENV-001"),
            "Large envelope",
            reorderLevel);
    }
}
