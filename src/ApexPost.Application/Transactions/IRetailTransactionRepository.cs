using ApexPost.Application.Persistence;
using ApexPost.Domain.Common;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Transactions;

public interface IRetailTransactionRepository
{
    Task<IReadOnlyList<Versioned<RetailTransaction>>> ListAsync(CancellationToken cancellationToken);
    Task<Versioned<RetailTransaction>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken);
    Task<bool> TryAddAsync(RetailTransaction transaction, CancellationToken cancellationToken);
    Task<bool> TryUpdateAsync(RetailTransaction transaction, long expectedVersion, CancellationToken cancellationToken);
}
