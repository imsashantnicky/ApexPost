using ApexPost.Domain.Common;

namespace ApexPost.Domain.Inventory;

public sealed class StockMovement
{
    internal StockMovement(EntityId id, StockMovementType type, int quantityChange, string reason, DateTimeOffset occurredAt)
    {
        Id = id;
        Type = type;
        QuantityChange = quantityChange;
        Reason = reason;
        OccurredAt = occurredAt;
    }

    public EntityId Id { get; }

    public StockMovementType Type { get; }

    public int QuantityChange { get; }

    public string Reason { get; }

    public DateTimeOffset OccurredAt { get; }
}
