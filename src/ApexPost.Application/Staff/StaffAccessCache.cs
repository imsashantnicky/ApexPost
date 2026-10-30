using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;
using Microsoft.Extensions.Caching.Memory;

namespace ApexPost.Application.Staff;

/// <summary>The fields an authorization check needs from a staff record, copied so cached values cannot be mutated.</summary>
public sealed record StaffAccessSnapshot(Guid StaffId, Guid BranchId, EmploymentStatus Status, IReadOnlyCollection<StaffRole> Roles);

/// <summary>
/// Caches staff lookups made on every authorized request for <see cref="Lifetime"/>, so each page no longer
/// re-reads staff.json once per policy evaluation. Staff updates and deactivations invalidate their entry
/// immediately, so a deactivated account loses access on its next request.
/// </summary>
public sealed class StaffAccessCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private readonly IStaffRepository repository;
    private readonly IMemoryCache cache;

    public StaffAccessCache(IStaffRepository repository, IMemoryCache cache)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<StaffAccessSnapshot?> GetAsync(Guid staffId, CancellationToken cancellationToken)
    {
        if (staffId == Guid.Empty) return null;
        if (cache.TryGetValue(Key(staffId), out StaffAccessSnapshot? cached)) return cached;

        var staff = await repository.GetByIdAsync(EntityId.From(staffId), cancellationToken);
        if (staff is null) return null;

        var snapshot = new StaffAccessSnapshot(staff.Value.Id.Value, staff.Value.BranchId.Value, staff.Value.Status, staff.Value.Roles.ToArray());
        cache.Set(Key(staffId), snapshot, Lifetime);
        return snapshot;
    }

    public void Invalidate(Guid staffId) => cache.Remove(Key(staffId));

    private static string Key(Guid staffId) => $"staff-access:{staffId:N}";
}
