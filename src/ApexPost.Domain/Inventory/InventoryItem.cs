using ApexPost.Domain.Common;

namespace ApexPost.Domain.Inventory;

public sealed class InventoryItem
{
    private readonly List<StockMovement> movements = [];

    public InventoryItem(EntityId id, EntityId branchId, InventorySku sku, string name, int reorderLevel)
    {
        Id = id ?? throw new DomainException("Inventory item identity is required.");
        BranchId = branchId ?? throw new DomainException("Branch identity is required.");
        Sku = sku ?? throw new DomainException("Inventory SKU is required.");
        Name = RequiredName(name);
        ReorderLevel = ValidNonNegativeQuantity(reorderLevel, "Reorder level");
    }

    public EntityId Id { get; }

    public EntityId BranchId { get; }

    public InventorySku Sku { get; }

    public string Name { get; private set; }

    public int ReorderLevel { get; private set; }

    public int QuantityOnHand { get; private set; }

    public IReadOnlyList<StockMovement> Movements => movements.AsReadOnly();

    public bool NeedsReorder => QuantityOnHand <= ReorderLevel;

    public void Rename(string name)
    {
        Name = RequiredName(name);
    }

    public void SetReorderLevel(int reorderLevel)
    {
        ReorderLevel = ValidNonNegativeQuantity(reorderLevel, "Reorder level");
    }

    public void Receive(int quantity, string reason, DateTimeOffset occurredAt)
    {
        ApplyMovement(StockMovementType.Receipt, ValidPositiveQuantity(quantity), reason, occurredAt);
    }

    public void Issue(int quantity, string reason, DateTimeOffset occurredAt)
    {
        var validQuantity = ValidPositiveQuantity(quantity);

        if (validQuantity > QuantityOnHand)
        {
            throw new DomainException("Stock cannot be issued below zero.");
        }

        ApplyMovement(StockMovementType.Issue, -validQuantity, reason, occurredAt);
    }

    public void AdjustTo(int quantity, string reason, DateTimeOffset occurredAt)
    {
        var adjustedQuantity = ValidNonNegativeQuantity(quantity, "Adjusted quantity");
        var quantityChange = adjustedQuantity - QuantityOnHand;

        if (quantityChange == 0)
        {
            throw new DomainException("Inventory adjustment must change the quantity on hand.");
        }

        var type = quantityChange > 0 ? StockMovementType.AdjustmentIncrease : StockMovementType.AdjustmentDecrease;
        ApplyMovement(type, quantityChange, reason, occurredAt);
    }

    private void ApplyMovement(StockMovementType type, int quantityChange, string reason, DateTimeOffset occurredAt)
    {
        var normalizedReason = reason?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > 250)
        {
            throw new DomainException("Stock movement reason is required and must not exceed 250 characters.");
        }

        if (occurredAt == default)
        {
            throw new DomainException("Stock movement time is required.");
        }

        if (quantityChange > 0 && QuantityOnHand > int.MaxValue - quantityChange)
        {
            throw new DomainException("Stock quantity exceeds the supported maximum.");
        }

        QuantityOnHand += quantityChange;
        movements.Add(new StockMovement(EntityId.New(), type, quantityChange, normalizedReason, occurredAt));
    }

    private static int ValidPositiveQuantity(int quantity)
    {
        if (quantity < 1)
        {
            throw new DomainException("Stock quantity must be greater than zero.");
        }

        return quantity;
    }

    private static int ValidNonNegativeQuantity(int quantity, string fieldName)
    {
        if (quantity < 0)
        {
            throw new DomainException($"{fieldName} cannot be negative.");
        }

        return quantity;
    }

    private static string RequiredName(string name)
    {
        var normalizedName = name?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 120)
        {
            throw new DomainException("Inventory item name is required and must not exceed 120 characters.");
        }

        return normalizedName;
    }
}
