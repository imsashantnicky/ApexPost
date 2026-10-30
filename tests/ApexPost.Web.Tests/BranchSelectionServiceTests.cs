using ApexPost.Application.Branches;
using ApexPost.Application.Persistence;
using ApexPost.Application.Security;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Web.Models;
using ApexPost.Web.Services;

namespace ApexPost.Web.Tests;

public sealed class BranchSelectionServiceTests
{
    private static readonly Branch Open = Create("OPN-001", "Open branch", BranchStatus.Active);
    private static readonly Branch Other = Create("OTH-001", "Another branch", BranchStatus.Active);
    private static readonly Branch Closed = Create("CLS-001", "Closed branch", BranchStatus.Closed);

    [Fact]
    public async Task Administrator_SeesEveryActiveBranchAndKeepsTheirChoice()
    {
        var form = new TransactionCreateForm { BranchId = Other.Id.Value };
        var service = Service(Roles.Administrator, Open.Id.Value);

        await service.PopulateAsync(form, default);
        service.ApplyActorBranch(form);

        Assert.Equal(["Another branch", "Open branch"], form.Branches.Select(branch => branch.Name));
        Assert.Equal(Other.Id.Value, form.BranchId);
    }

    [Fact]
    public async Task BranchStaff_ArePinnedToTheirAssignedBranch()
    {
        var form = new MoneyOrderForm { BranchId = Other.Id.Value };
        var service = Service(Roles.CounterStaff, Open.Id.Value);

        service.ApplyActorBranch(form);
        Assert.Equal(Open.Id.Value, form.BranchId);

        await service.PopulateAsync(form, default);
        Assert.Equal(Open.Id.Value, Assert.Single(form.Branches).Id);
    }

    [Fact]
    public async Task BranchStaff_AtClosedBranch_GetNoChoices()
    {
        var form = new InventoryForm();

        await Service(Roles.InventoryStaff, Closed.Id.Value).PopulateAsync(form, default);

        Assert.Empty(form.Branches);
        Assert.Equal(Closed.Id.Value, form.BranchId);
    }

    private static BranchSelectionService Service(string role, Guid assignedBranch) =>
        new(new Branches([Open, Other, Closed]), new FixedActor(new ActorContext(Guid.NewGuid(), role, assignedBranch, "branch-selection-test")));

    private static Branch Create(string code, string name, BranchStatus status) =>
        Branch.Restore(EntityId.New(), BranchCode.From(code), name, new BranchAddress("1 Road", null, "City", "12345", "IN"), 1, status);

    private sealed class FixedActor(ActorContext actor) : ICurrentActor { public ActorContext? Actor => actor; }

    private sealed class Branches(IReadOnlyList<Branch> branches) : IBranchRepository
    {
        public Task<IReadOnlyList<Versioned<Branch>>> ListAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<Versioned<Branch>>>(branches.Select(branch => new Versioned<Branch>(branch, 1)).ToArray());
        public Task<Versioned<Branch>?> GetByIdAsync(EntityId id, CancellationToken token) => Task.FromResult(branches.Where(branch => branch.Id == id).Select(branch => (Versioned<Branch>?)new(branch, 1)).SingleOrDefault());
        public Task<bool> TryAddAsync(Branch value, CancellationToken token) => throw new NotSupportedException();
        public Task<bool> TryUpdateAsync(Branch value, long version, CancellationToken token) => throw new NotSupportedException();
    }
}
