using Microsoft.Extensions.Caching.Memory;
using ApexPost.Application.Branches;
using ApexPost.Application.Catalogue;
using ApexPost.Application.Persistence;
using ApexPost.Application.Staff;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;

namespace ApexPost.Application.Tests;

public class ManagementUseCaseTests
{
    [Fact]
    public async Task BranchCreateRetrieveUpdateAndDeactivateFollowDomainRules()
    {
        var repository = new BranchRepositoryDouble();
        var useCase = NewBranchUseCase(repository);
        var created = await useCase.CreateAsync(new("BLR-001", "Central", "1 Market Road", null, "Bengaluru", "560001", "IN", 2), default);

        var fetched = await useCase.GetAsync(created.Value.Id, default);
        var updated = await useCase.UpdateAsync(new(created.Value.Id, fetched.Value.Version, "Central Office", "2 Market Road", null, "Bengaluru", "560001", "IN", 3), default);
        var deactivated = await useCase.DeactivateAsync(created.Value.Id, updated.Value.Version, default);

        Assert.True(created.IsSuccess);
        Assert.Equal("Central Office", updated.Value.Name);
        Assert.Equal(BranchStatus.Suspended, deactivated.Value.Status);
    }

    [Fact]
    public async Task BranchCreateRejectsDuplicateCodeAndUpdateReportsConcurrencyConflict()
    {
        var repository = new BranchRepositoryDouble();
        var useCase = NewBranchUseCase(repository);
        var created = await useCase.CreateAsync(new("BLR-001", "Central", "1 Road", null, "Bengaluru", "560001", "IN", 1), default);

        var duplicate = await useCase.CreateAsync(new("blr-001", "Other", "2 Road", null, "Bengaluru", "560001", "IN", 1), default);
        var conflict = await useCase.UpdateAsync(new(created.Value.Id, 9, "Changed", "1 Road", null, "Bengaluru", "560001", "IN", 1), default);

        Assert.Equal("identifier.duplicate", Assert.Single(duplicate.Errors).Code);
        Assert.Equal("concurrency.conflict", Assert.Single(conflict.Errors).Code);
    }

    [Fact]
    public async Task BranchCreateMapsDomainValidationToFailure()
    {
        var result = await NewBranchUseCase(new BranchRepositoryDouble())
            .CreateAsync(new("BAD", "", "", null, "", "", "", 0), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("domain.rule", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task StaffCreateRetrieveUpdateAndDeactivateFollowDomainRules()
    {
        var repository = new StaffRepositoryDouble();
        var useCase = NewStaffUseCase(repository);
        var branchId = Guid.NewGuid();
        var created = await useCase.CreateAsync(new("EMP-0001", "Asha Rao", branchId, [StaffRole.CounterClerk]), default);

        var fetched = await useCase.GetAsync(created.Value.Id, default);
        var updated = await useCase.UpdateAsync(new(created.Value.Id, fetched.Value.Version, "Asha Sharma", Guid.NewGuid(), [StaffRole.BranchManager]), default);
        var deactivated = await useCase.DeactivateAsync(created.Value.Id, updated.Value.Version, default);

        Assert.Equal("Asha Sharma", updated.Value.Name);
        Assert.Equal([StaffRole.BranchManager], updated.Value.Roles);
        Assert.Equal(EmploymentStatus.Suspended, deactivated.Value.Status);
    }

    [Fact]
    public async Task StaffCreateRejectsDuplicateEmployeeNumber()
    {
        var useCase = NewStaffUseCase(new StaffRepositoryDouble());
        await useCase.CreateAsync(new("EMP-0001", "Asha Rao", Guid.NewGuid(), [StaffRole.CounterClerk]), default);

        var result = await useCase.CreateAsync(new("emp-0001", "Rahul Verma", Guid.NewGuid(), [StaffRole.CounterClerk]), default);

        Assert.Equal("identifier.duplicate", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task CatalogueCreateRetrieveUpdateAndDeactivateFollowDomainRules()
    {
        var repository = new CatalogueRepositoryDouble();
        var useCase = new CatalogueManagementUseCase(repository);
        var created = await useCase.CreateAsync(new("EXP-001", "Express", "Priority delivery."), default);

        var fetched = await useCase.GetAsync(created.Value.Id, default);
        var updated = await useCase.UpdateAsync(new(created.Value.Id, fetched.Value.Version, "Express Parcel", "Priority parcel delivery."), default);
        var deactivated = await useCase.DeactivateAsync(created.Value.Id, updated.Value.Version, default);

        Assert.Equal("Express Parcel", updated.Value.Name);
        Assert.Equal(ServiceAvailability.Unavailable, deactivated.Value.Availability);
    }

    [Fact]
    public async Task CatalogueCreateRejectsDuplicateCodeAndInvalidUpdate()
    {
        var useCase = new CatalogueManagementUseCase(new CatalogueRepositoryDouble());
        var created = await useCase.CreateAsync(new("EXP-001", "Express", "Priority delivery."), default);
        var duplicate = await useCase.CreateAsync(new("exp-001", "Other", "Other delivery."), default);
        var invalid = await useCase.UpdateAsync(new(created.Value.Id, 1, "", ""), default);

        Assert.Equal("identifier.duplicate", Assert.Single(duplicate.Errors).Code);
        Assert.Equal("domain.rule", Assert.Single(invalid.Errors).Code);
    }

    private sealed class BranchRepositoryDouble : IBranchRepository
    {
        private readonly Dictionary<Guid, Versioned<Branch>> items = [];
        public Task<IReadOnlyList<Versioned<Branch>>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Versioned<Branch>>>(items.Values.ToArray());
        public Task<Versioned<Branch>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken) => Task.FromResult(items.GetValueOrDefault(id.Value));
        public Task<bool> TryAddAsync(Branch branch, CancellationToken cancellationToken)
        {
            if (items.Values.Any(item => item.Value.Code == branch.Code)) return Task.FromResult(false);
            items.Add(branch.Id.Value, new(branch, 1)); return Task.FromResult(true);
        }
        public Task<bool> TryUpdateAsync(Branch branch, long expectedVersion, CancellationToken cancellationToken)
        {
            if (!items.TryGetValue(branch.Id.Value, out var current) || current.Version != expectedVersion) return Task.FromResult(false);
            items[branch.Id.Value] = new(branch, expectedVersion + 1); return Task.FromResult(true);
        }
    }

    private static BranchManagementUseCase NewBranchUseCase(IBranchRepository repository) => new(repository, new TestActor(), new InMemoryAuditStore().Trail());
    private static StaffManagementUseCase NewStaffUseCase(IStaffRepository repository) => new(repository, new TestActor(), new InMemoryAuditStore().Trail(), new StaffAccessCache(repository, new MemoryCache(new MemoryCacheOptions())));
    private sealed class TestActor : ApexPost.Application.Security.ICurrentActor
    {
        public ApexPost.Application.Security.ActorContext? Actor { get; } = new(Guid.NewGuid(), "Administrator", Guid.NewGuid(), "management-test");
    }

    private sealed class StaffRepositoryDouble : IStaffRepository
    {
        private readonly Dictionary<Guid, Versioned<StaffMember>> items = [];
        public Task<IReadOnlyList<Versioned<StaffMember>>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Versioned<StaffMember>>>(items.Values.ToArray());
        public Task<Versioned<StaffMember>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken) => Task.FromResult(items.GetValueOrDefault(id.Value));
        public Task<bool> TryAddAsync(StaffMember staffMember, CancellationToken cancellationToken)
        {
            if (items.Values.Any(item => item.Value.EmployeeNumber == staffMember.EmployeeNumber)) return Task.FromResult(false);
            items.Add(staffMember.Id.Value, new(staffMember, 1)); return Task.FromResult(true);
        }
        public Task<bool> TryUpdateAsync(StaffMember staffMember, long expectedVersion, CancellationToken cancellationToken)
        {
            if (!items.TryGetValue(staffMember.Id.Value, out var current) || current.Version != expectedVersion) return Task.FromResult(false);
            items[staffMember.Id.Value] = new(staffMember, expectedVersion + 1); return Task.FromResult(true);
        }
    }

    private sealed class CatalogueRepositoryDouble : IServiceDefinitionRepository
    {
        private readonly Dictionary<Guid, Versioned<ServiceDefinition>> items = [];
        public Task<IReadOnlyList<Versioned<ServiceDefinition>>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Versioned<ServiceDefinition>>>(items.Values.ToArray());
        public Task<Versioned<ServiceDefinition>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken) => Task.FromResult(items.GetValueOrDefault(id.Value));
        public Task<bool> TryAddAsync(ServiceDefinition serviceDefinition, CancellationToken cancellationToken)
        {
            if (items.Values.Any(item => item.Value.Code == serviceDefinition.Code)) return Task.FromResult(false);
            items.Add(serviceDefinition.Id.Value, new(serviceDefinition, 1)); return Task.FromResult(true);
        }
        public Task<bool> TryUpdateAsync(ServiceDefinition serviceDefinition, long expectedVersion, CancellationToken cancellationToken)
        {
            if (!items.TryGetValue(serviceDefinition.Id.Value, out var current) || current.Version != expectedVersion) return Task.FromResult(false);
            items[serviceDefinition.Id.Value] = new(serviceDefinition, expectedVersion + 1); return Task.FromResult(true);
        }
    }
}
