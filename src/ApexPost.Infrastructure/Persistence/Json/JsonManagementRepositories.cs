using ApexPost.Application.Branches;
using ApexPost.Application.Catalogue;
using ApexPost.Application.Persistence;
using ApexPost.Application.Staff;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;

namespace ApexPost.Infrastructure.Persistence.Json;

#pragma warning disable CA1725 // Compact adapter parameter names are intentional.

public sealed class JsonBranchRepository : IBranchRepository
{
    private readonly JsonCollectionStore<BranchData> store;
    public JsonBranchRepository(JsonDataOptions options) => store = new(options.DataDirectory, "branches.json");
    public async Task<IReadOnlyList<Versioned<Branch>>> ListAsync(CancellationToken token) => (await store.ReadAllAsync(token)).Values.Select(ToDomain).ToArray();
    public async Task<Versioned<Branch>?> GetByIdAsync(EntityId id, CancellationToken token) => (await store.ReadAllAsync(token)).TryGetValue(id.Value.ToString("N"), out var data) ? ToDomain(data) : null;
    public Task<bool> TryAddAsync(Branch branch, CancellationToken token) => store.TryAddUniqueAsync(branch.Id.Value.ToString("N"), ToData(branch, 1), x => x.Code == branch.Code.Value, token);
    public Task<bool> TryUpdateAsync(Branch branch, long version, CancellationToken token) => store.TryReplaceAsync(branch.Id.Value.ToString("N"), ToData(branch, version + 1), data => data.Version == version, token);
    private static Versioned<Branch> ToDomain(BranchData x) => new(Branch.Restore(EntityId.From(x.Id), BranchCode.From(x.Code), x.Name, new BranchAddress(x.Line1, x.Line2, x.City, x.PostalCode, x.CountryCode), x.CounterCount, x.Status), x.Version);
    private static BranchData ToData(Branch x, long v) => new(x.Id.Value, v, x.Code.Value, x.Name, x.Address.Line1, x.Address.Line2, x.Address.City, x.Address.PostalCode, x.Address.CountryCode, x.CounterCount, x.Status);
    private sealed record BranchData(Guid Id, long Version, string Code, string Name, string Line1, string? Line2, string City, string PostalCode, string CountryCode, int CounterCount, BranchStatus Status);
}

public sealed class JsonStaffRepository : IStaffRepository
{
    private readonly JsonCollectionStore<StaffData> store;
    public JsonStaffRepository(JsonDataOptions options) => store = new(options.DataDirectory, "staff.json");
    public async Task<IReadOnlyList<Versioned<StaffMember>>> ListAsync(CancellationToken token) => (await store.ReadAllAsync(token)).Values.Select(ToDomain).ToArray();
    public async Task<Versioned<StaffMember>?> GetByIdAsync(EntityId id, CancellationToken token) => (await store.ReadAllAsync(token)).TryGetValue(id.Value.ToString("N"), out var data) ? ToDomain(data) : null;
    public Task<bool> TryAddAsync(StaffMember staff, CancellationToken token) => store.TryAddUniqueAsync(staff.Id.Value.ToString("N"), ToData(staff, 1), x => x.EmployeeNumber == staff.EmployeeNumber.Value, token);
    public Task<bool> TryUpdateAsync(StaffMember staff, long version, CancellationToken token) => store.TryReplaceAsync(staff.Id.Value.ToString("N"), ToData(staff, version + 1), data => data.Version == version, token);
    private static Versioned<StaffMember> ToDomain(StaffData x) => new(StaffMember.Restore(EntityId.From(x.Id), EmployeeNumber.From(x.EmployeeNumber), x.Name, EntityId.From(x.BranchId), x.Roles, x.Status), x.Version);
    private static StaffData ToData(StaffMember x, long v) => new(x.Id.Value, v, x.EmployeeNumber.Value, x.Name, x.BranchId.Value, x.Roles.ToArray(), x.Status);
    private sealed record StaffData(Guid Id, long Version, string EmployeeNumber, string Name, Guid BranchId, StaffRole[] Roles, EmploymentStatus Status);
}

public sealed class JsonServiceDefinitionRepository : IServiceDefinitionRepository
{
    private readonly JsonCollectionStore<ServiceData> store;
    public JsonServiceDefinitionRepository(JsonDataOptions options) => store = new(options.DataDirectory, "catalogue.json");
    public async Task<IReadOnlyList<Versioned<ServiceDefinition>>> ListAsync(CancellationToken token) => (await store.ReadAllAsync(token)).Values.Select(ToDomain).ToArray();
    public async Task<Versioned<ServiceDefinition>?> GetByIdAsync(EntityId id, CancellationToken token) => (await store.ReadAllAsync(token)).TryGetValue(id.Value.ToString("N"), out var data) ? ToDomain(data) : null;
    public Task<bool> TryAddAsync(ServiceDefinition service, CancellationToken token) => store.TryAddUniqueAsync(service.Id.Value.ToString("N"), ToData(service, 1), x => x.Code == service.Code.Value, token);
    public Task<bool> TryUpdateAsync(ServiceDefinition service, long version, CancellationToken token) => store.TryReplaceAsync(service.Id.Value.ToString("N"), ToData(service, version + 1), data => data.Version == version, token);
    private static Versioned<ServiceDefinition> ToDomain(ServiceData x) => new(ServiceDefinition.Restore(EntityId.From(x.Id), ServiceCode.From(x.Code), x.Name, x.Description, x.Availability), x.Version);
    private static ServiceData ToData(ServiceDefinition x, long v) => new(x.Id.Value, v, x.Code.Value, x.Name, x.Description, x.Availability);
    private sealed record ServiceData(Guid Id, long Version, string Code, string Name, string Description, ServiceAvailability Availability);
}

#pragma warning restore CA1725
