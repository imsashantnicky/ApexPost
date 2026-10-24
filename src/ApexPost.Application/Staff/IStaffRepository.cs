using ApexPost.Application.Persistence;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;

namespace ApexPost.Application.Staff;

public interface IStaffRepository
{
    Task<IReadOnlyList<Versioned<StaffMember>>> ListAsync(CancellationToken cancellationToken);
    Task<Versioned<StaffMember>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken);
    Task<bool> TryAddAsync(StaffMember staffMember, CancellationToken cancellationToken);
    Task<bool> TryUpdateAsync(StaffMember staffMember, long expectedVersion, CancellationToken cancellationToken);
}
