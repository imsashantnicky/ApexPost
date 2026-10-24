using ApexPost.Application.Persistence;
using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;

namespace ApexPost.Application.Shipping;

public interface IShipmentRepository
{
    Task<IReadOnlyList<Versioned<Shipment>>> ListAsync(CancellationToken cancellationToken);
    Task<Versioned<Shipment>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken);
    Task<bool> TryAddAsync(Shipment shipment, CancellationToken cancellationToken);
    Task<bool> TryUpdateAsync(Shipment shipment, long expectedVersion, CancellationToken cancellationToken);
}
