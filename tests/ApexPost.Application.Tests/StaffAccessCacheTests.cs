using ApexPost.Application.Persistence;
using ApexPost.Application.Security;
using ApexPost.Application.Staff;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;
using Microsoft.Extensions.Caching.Memory;

namespace ApexPost.Application.Tests;

public sealed class StaffAccessCacheTests
{
    [Fact]
    public async Task RepeatedLookupsWithinTheLifetimeReadTheRepositoryOnce()
    {
        var repository = new CountingStaffRepository();
        var cache = NewCache(repository);

        for (var request = 0; request < 5; request++) Assert.NotNull(await cache.GetAsync(repository.StaffId, default));

        Assert.Equal(1, repository.Reads);
    }

    [Fact]
    public async Task InvalidateForcesTheNextLookupToReload()
    {
        var repository = new CountingStaffRepository();
        var cache = NewCache(repository);
        await cache.GetAsync(repository.StaffId, default);

        cache.Invalidate(repository.StaffId);
        await cache.GetAsync(repository.StaffId, default);

        Assert.Equal(2, repository.Reads);
    }

    [Fact]
    public async Task DeactivatingStaffIsVisibleOnTheNextLookup()
    {
        var repository = new CountingStaffRepository();
        var cache = NewCache(repository);
        var useCase = new StaffManagementUseCase(repository, new AdministratorActor(), new InMemoryAuditStore().Trail(), cache);
        Assert.Equal(EmploymentStatus.Active, (await cache.GetAsync(repository.StaffId, default))!.Status);

        Assert.True((await useCase.DeactivateAsync(repository.StaffId, 1, default)).IsSuccess);

        Assert.Equal(EmploymentStatus.Suspended, (await cache.GetAsync(repository.StaffId, default))!.Status);
    }

    [Fact]
    public async Task UpdatingStaffRolesIsVisibleOnTheNextLookup()
    {
        var repository = new CountingStaffRepository();
        var cache = NewCache(repository);
        var useCase = new StaffManagementUseCase(repository, new AdministratorActor(), new InMemoryAuditStore().Trail(), cache);
        await cache.GetAsync(repository.StaffId, default);

        var result = await useCase.UpdateAsync(new UpdateStaffRequest(repository.StaffId, 1, "Renamed", repository.BranchId, [StaffRole.InventoryClerk]), default);

        Assert.True(result.IsSuccess);
        Assert.Equal([StaffRole.InventoryClerk], (await cache.GetAsync(repository.StaffId, default))!.Roles);
    }

    private static StaffAccessCache NewCache(IStaffRepository repository) => new(repository, new MemoryCache(new MemoryCacheOptions()));

    private sealed class AdministratorActor : ICurrentActor
    {
        public ActorContext? Actor { get; } = new(Guid.NewGuid(), Roles.Administrator, Guid.NewGuid(), "staff-cache-test");
    }

    private sealed class CountingStaffRepository : IStaffRepository
    {
        private Versioned<StaffMember> current;

        public CountingStaffRepository() =>
            current = new(new StaffMember(EntityId.From(StaffId), EmployeeNumber.From("EMP-9100"), "Cached Staff", EntityId.From(BranchId), [StaffRole.CounterClerk]), 1);

        public Guid StaffId { get; } = Guid.NewGuid();
        public Guid BranchId { get; } = Guid.NewGuid();
        public int Reads { get; private set; }

        public Task<IReadOnlyList<Versioned<StaffMember>>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Versioned<StaffMember>>>([current]);

        public Task<Versioned<StaffMember>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken)
        {
            Reads++;
            if (id.Value != StaffId) return Task.FromResult<Versioned<StaffMember>?>(null);
            // Hand out a fresh copy, as the JSON repository does, so callers cannot mutate stored state.
            var copy = StaffMember.Restore(current.Value.Id, current.Value.EmployeeNumber, current.Value.Name, current.Value.BranchId, current.Value.Roles, current.Value.Status);
            return Task.FromResult<Versioned<StaffMember>?>(new(copy, current.Version));
        }

        public Task<bool> TryAddAsync(StaffMember staffMember, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> TryUpdateAsync(StaffMember staffMember, long expectedVersion, CancellationToken cancellationToken)
        {
            if (current.Version != expectedVersion) return Task.FromResult(false);
            current = new(staffMember, expectedVersion + 1);
            return Task.FromResult(true);
        }
    }
}
