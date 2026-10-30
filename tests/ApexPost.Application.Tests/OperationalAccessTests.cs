using ApexPost.Application.Branches;
using ApexPost.Application.Persistence;
using ApexPost.Application.Security;
using ApexPost.Application.Staff;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;

namespace ApexPost.Application.Tests;

public sealed class OperationalAccessTests
{
    [Fact]
    public async Task ActiveStaffCanAccessTheirOpenBranch()
    {
        var fixture = new Fixture();
        var result = await fixture.Access.CheckAsync(fixture.BranchId, OperationalCapability.Counter, "Test.SameBranch", default);
        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(EmploymentStatus.Suspended)]
    [InlineData(EmploymentStatus.Terminated)]
    public async Task InactiveStaffAreRejected(EmploymentStatus status)
    {
        var fixture = new Fixture(status: status);
        var result = await fixture.Access.CheckAsync(fixture.BranchId, OperationalCapability.Counter, "Test.Inactive", default);
        Assert.Equal("actor.inactive", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task MissingStaffIsRejected()
    {
        var fixture = new Fixture(staffExists: false);
        var result = await fixture.Access.CheckAsync(fixture.BranchId, OperationalCapability.Counter, "Test.MissingStaff", default);
        Assert.Equal("actor.not-found", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task OrdinaryStaffCannotCrossBranches()
    {
        var fixture = new Fixture(role: "CounterStaff");
        var requestedBranchId = Guid.NewGuid();
        var result = await fixture.Access.CheckAsync(requestedBranchId, OperationalCapability.Counter, "Test.CrossBranch", default);
        Assert.Equal("access.denied", Assert.Single(result.Errors).Code);
        Assert.Equal((requestedBranchId, "Test.CrossBranch:Denied"), Assert.Single(fixture.Operations));
    }

    [Fact]
    public async Task MissingBranchIsRejected()
    {
        var fixture = new Fixture(branchExists: false);
        var result = await fixture.Access.CheckAsync(fixture.BranchId, OperationalCapability.Counter, "Test.MissingBranch", default);
        Assert.Equal("branch.not-found", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task ClosedBranchIsRejectedForWrites()
    {
        var fixture = new Fixture(branchStatus: BranchStatus.Closed);
        var result = await fixture.Access.CheckAsync(fixture.BranchId, OperationalCapability.Counter, "Test.ClosedBranch", default);
        Assert.Equal("branch.closed", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task AdministratorCrossBranchAccessIsExplicitlyAudited()
    {
        var fixture = new Fixture(role: "Administrator");
        var otherBranch = Guid.NewGuid();
        var result = await fixture.Access.CheckAsync(otherBranch, OperationalCapability.Counter, "MoneyOrder.Issue", default);
        Assert.True(result.IsSuccess);
        Assert.Equal([(otherBranch, "MoneyOrder.Issue")], fixture.Operations);
    }

    [Theory]
    [InlineData("Transaction.StartPayment")]
    [InlineData("Transaction.PaymentConfirmed")]
    [InlineData("Transaction.RetryPayment")]
    [InlineData("IdentityVerification.Complete")]
    public async Task AdministratorCrossBranch_PaymentAndIdentityOperations_Succeed(string operation)
    {
        var fixture = new Fixture(role: "Administrator");
        var otherBranch = Guid.NewGuid();

        var result = await fixture.Access.CheckAsync(otherBranch, OperationalCapability.Counter, operation, default);

        Assert.True(result.IsSuccess);
        Assert.Equal([(otherBranch, operation)], fixture.Operations);
    }

    [Fact]
    public async Task AuditorCannotPerformOperationalWrites()
    {
        var fixture = new Fixture(role: "Auditor");
        var result = await fixture.Access.CheckAsync(fixture.BranchId, OperationalCapability.Counter, "MoneyOrder.Issue", default);
        Assert.Equal("access.denied", Assert.Single(result.Errors).Code);
    }

    private sealed class Fixture
    {
        public Fixture(string role = "CounterStaff", EmploymentStatus status = EmploymentStatus.Active, bool staffExists = true, bool branchExists = true, BranchStatus branchStatus = BranchStatus.Active)
        {
            BranchId = Guid.NewGuid();
            StaffId = Guid.NewGuid();
            var staff = new StaffMember(EntityId.From(StaffId), EmployeeNumber.From("EMP-9002"), "Test Staff", EntityId.From(BranchId), [StaffRole.CounterClerk]);
            if (status == EmploymentStatus.Suspended) staff.Suspend();
            if (status == EmploymentStatus.Terminated) staff.Terminate();
            var actor = new ActorContext(StaffId, role, BranchId, "corr-test");
            Audit = new InMemoryAuditStore();
            Access = new OperationalAccess(new CurrentActor(actor), new StaffRepository(staff, staffExists), new BranchRepository(branchExists, branchStatus), Audit.Trail());
        }
        public Guid BranchId { get; }
        public Guid StaffId { get; }
        public OperationalAccess Access { get; }
        public InMemoryAuditStore Audit { get; }

        // Cross-branch entries are keyed by the operation they authorized; everything else by action and outcome.
        public IReadOnlyList<(Guid BranchId, string Operation)> Operations => Audit.Entries
            .Select(entry => entry.Action == "Administrator.CrossBranchAccess"
                ? (entry.BranchId ?? Guid.Empty, entry.Metadata["Operation"])
                : (entry.BranchId ?? Guid.Empty, $"{entry.Action}:{entry.Outcome}"))
            .ToArray();
    }

    private sealed class CurrentActor(ActorContext actor) : ICurrentActor { public ActorContext? Actor => actor; }
    private sealed class StaffRepository(StaffMember staff, bool exists) : IStaffRepository
    {
        public Task<IReadOnlyList<Versioned<StaffMember>>> ListAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<Versioned<StaffMember>>>([]);
        public Task<Versioned<StaffMember>?> GetByIdAsync(EntityId id, CancellationToken token) => Task.FromResult<Versioned<StaffMember>?>(exists && id == staff.Id ? new(staff, 1) : null);
        public Task<bool> TryAddAsync(StaffMember value, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(StaffMember value, long version, CancellationToken token) => Task.FromResult(false);
    }
    private sealed class BranchRepository(bool exists, BranchStatus status) : IBranchRepository
    {
        public Task<IReadOnlyList<Versioned<Branch>>> ListAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<Versioned<Branch>>>([]);
        public Task<Versioned<Branch>?> GetByIdAsync(EntityId id, CancellationToken token)
        {
            if (!exists) return Task.FromResult<Versioned<Branch>?>(null);
            var branch = Branch.Restore(id, BranchCode.From("TST-002"), "Test Branch", new BranchAddress("1 Test Road", null, "Test City", "12345", "IN"), 1, status);
            return Task.FromResult<Versioned<Branch>?>(new(branch, 1));
        }
        public Task<bool> TryAddAsync(Branch value, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(Branch value, long version, CancellationToken token) => Task.FromResult(false);
    }
}
