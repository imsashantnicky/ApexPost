using ApexPost.Application.Persistence;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;

namespace ApexPost.Application.Inventory;

public interface IInventoryItemRepository
{
    Task<Versioned<InventoryItem>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken);
    Task<bool> TryUpdateAsync(InventoryItem item, long expectedVersion, CancellationToken cancellationToken);
}
