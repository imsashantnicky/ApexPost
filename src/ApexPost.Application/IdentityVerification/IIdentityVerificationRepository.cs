using ApexPost.Application.Persistence;
using ApexPost.Domain.Common;
using ApexPost.Domain.IdentityVerification;

namespace ApexPost.Application.IdentityVerification;

public interface IIdentityVerificationRepository
{
    Task<IReadOnlyList<Versioned<IdentityVerificationRecord>>> ListAsync(CancellationToken cancellationToken);
    Task<Versioned<IdentityVerificationRecord>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken);
    Task<bool> TryAddAsync(IdentityVerificationRecord record, CancellationToken cancellationToken);
    Task<bool> TryUpdateAsync(IdentityVerificationRecord record, long expectedVersion, CancellationToken cancellationToken);
}
