using ApexPost.Application.Persistence;
using ApexPost.Domain.Common;
using ApexPost.Domain.MoneyOrders;

namespace ApexPost.Application.MoneyOrders;

public interface IMoneyOrderRepository
{
    Task<IReadOnlyList<Versioned<MoneyOrder>>> ListAsync(CancellationToken cancellationToken);
    Task<Versioned<MoneyOrder>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken);
    Task<bool> TryAddAsync(MoneyOrder moneyOrder, CancellationToken cancellationToken);
    Task<bool> TryUpdateAsync(MoneyOrder moneyOrder, long expectedVersion, CancellationToken cancellationToken);
}
