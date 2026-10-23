using ApexPost.Application.Persistence;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;

namespace ApexPost.Application.Branches;

public interface IBranchRepository
{
    Task<Versioned<Branch>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken);
    Task<bool> TryAddAsync(Branch branch, CancellationToken cancellationToken);
    Task<bool> TryUpdateAsync(Branch branch, long expectedVersion, CancellationToken cancellationToken);
}
