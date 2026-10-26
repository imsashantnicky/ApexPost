using ApexPost.Application.Branches;
using ApexPost.Application.Persistence;
using ApexPost.Application.Security;
using ApexPost.Application.Staff;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;

namespace ApexPost.Application.Tests;

internal static class OperationalAccessFixture
{
    public static OperationalAccess Create(Guid? branchId = null, string role = "Administrator", EmploymentStatus status = EmploymentStatus.Active, Guid? staffId = null, ICurrentActor? currentActor = null, IAccessAuditWriter? auditWriter = null)
    {
        var assignedBranch = branchId ?? currentActor?.Actor?.AssignedBranchId ?? Guid.NewGuid();
        var actorId = staffId ?? currentActor?.Actor?.StaffId ?? Guid.NewGuid();
        var staff = new StaffMember(EntityId.From(actorId), EmployeeNumber.From("EMP-9001"), "Test Operator", EntityId.From(assignedBranch), [StaffRole.BranchManager]);
        if (status == EmploymentStatus.Suspended) staff.Suspend();
        if (status == EmploymentStatus.Terminated) staff.Terminate();
        return new OperationalAccess(currentActor ?? new FixedActor(new ActorContext(actorId, role, assignedBranch, "test-correlation")), new StaffRepo(staff), new BranchRepo(), auditWriter ?? new Audit());
    }

    private sealed class FixedActor(ActorContext? actor) : ICurrentActor { public ActorContext? Actor => actor; }

    private sealed class StaffRepo(StaffMember staff) : IStaffRepository
    {
        public Task<IReadOnlyList<Versioned<StaffMember>>> ListAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<Versioned<StaffMember>>>([new(staff, 1)]);
        public Task<Versioned<StaffMember>?> GetByIdAsync(EntityId id, CancellationToken token) => Task.FromResult<Versioned<StaffMember>?>(id == staff.Id ? new(staff, 1) : null);
        public Task<bool> TryAddAsync(StaffMember value, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(StaffMember value, long version, CancellationToken token) => Task.FromResult(false);
    }

    private sealed class BranchRepo : IBranchRepository
    {
        public Task<IReadOnlyList<Versioned<Branch>>> ListAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<Versioned<Branch>>>([]);
        public Task<Versioned<Branch>?> GetByIdAsync(EntityId id, CancellationToken token)
        {
            var branch = new Branch(id, BranchCode.From("TST-001"), "Test Branch", new BranchAddress("1 Test Road", null, "Test City", "12345", "IN"), 1);
            return Task.FromResult<Versioned<Branch>?>(new(branch, 1));
        }
        public Task<bool> TryAddAsync(Branch value, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(Branch value, long version, CancellationToken token) => Task.FromResult(false);
    }

    private sealed class Audit : IAccessAuditWriter
    {
        public Task WriteAdministratorCrossBranchAccessAsync(ActorContext actor, Guid branchId, string operation, CancellationToken token) => Task.CompletedTask;
        public Task WriteOperationAsync(ActorContext? actor, Guid? branchId, string operation, string targetType, string targetReference, string outcome, IReadOnlyDictionary<string, string>? safeMetadata, CancellationToken token) => Task.CompletedTask;
    }
}
