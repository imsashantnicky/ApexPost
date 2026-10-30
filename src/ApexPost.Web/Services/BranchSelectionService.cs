using ApexPost.Application.Branches;
using ApexPost.Application.Security;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Web.Models;

namespace ApexPost.Web.Services;

/// <summary>A form where the operator chooses (or is pinned to) the branch an operation runs at.</summary>
public interface IBranchSelectionForm
{
    Guid BranchId { get; set; }
    IReadOnlyList<BranchOption> Branches { get; set; }
}

/// <summary>
/// Fills branch pickers for counter and inventory forms. Administrators may choose any active branch;
/// everyone else is pinned to their assigned branch. OperationalAccess still authorizes the operation.
/// </summary>
public sealed class BranchSelectionService(IBranchRepository branches, ICurrentActor currentActor)
{
    public async Task PopulateAsync(IBranchSelectionForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var actor = currentActor.Actor;
        if (StaffRoleMapping.IsAdministrator(actor?.Role))
        {
            form.Branches = (await branches.ListAsync(cancellationToken)).Where(item => item.Value.Status == BranchStatus.Active)
                .Select(item => new BranchOption(item.Value.Id.Value, item.Value.Name)).OrderBy(item => item.Name).ToArray();
        }
        else if (actor?.AssignedBranchId is Guid branchId)
        {
            var branch = await branches.GetByIdAsync(EntityId.From(branchId), cancellationToken);
            form.Branches = branch is not null && branch.Value.Status == BranchStatus.Active ? [new BranchOption(branchId, branch.Value.Name)] : [];
            form.BranchId = branchId;
        }
    }

    /// <summary>Pins a non-administrator's submitted branch to their assigned branch.</summary>
    public void ApplyActorBranch(IBranchSelectionForm form)
    {
        ArgumentNullException.ThrowIfNull(form);
        var actor = currentActor.Actor;
        if (!StaffRoleMapping.IsAdministrator(actor?.Role) && actor?.AssignedBranchId is Guid branchId) form.BranchId = branchId;
    }
}
