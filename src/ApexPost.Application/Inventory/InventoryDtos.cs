namespace ApexPost.Application.Inventory;

public sealed record AdjustInventoryRequest(Guid InventoryItemId, long ExpectedVersion, int Quantity, string Reason, DateTimeOffset OccurredAt);
public sealed record CreateInventoryItemRequest(Guid BranchId, string Sku, string Name, int ReorderLevel);
public sealed record InventoryItemResponse(Guid Id, long Version, Guid BranchId, string Sku, string Name, int QuantityOnHand, int ReorderLevel, bool NeedsReorder);
