using ApexPost.Application.Auditing;
using ApexPost.Application.Branches;
using ApexPost.Application.Results;
using ApexPost.Application.Staff;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;

namespace ApexPost.Application.Security;

public sealed record ActorContext(Guid StaffId, string Role, Guid? AssignedBranchId, string CorrelationId);

public interface ICurrentActor
{
    ActorContext? Actor { get; }
}

public enum OperationalCapability { Inventory, Counter, BranchManager }

/// <summary>The branches a list read may show: every branch, or exactly one.</summary>
public sealed record BranchScope
{
    private BranchScope(Guid? branchId) => BranchId = branchId;

    public static BranchScope AllBranches { get; } = new((Guid?)null);

    public Guid? BranchId { get; }

    public static BranchScope OnlyBranch(Guid branchId) => new(branchId);

    public bool Includes(Guid branchId) => BranchId is null || BranchId == branchId;
}

public sealed class OperationalAccess
{
    public const string ActorRequired = "actor.required";
    public const string ActorInactive = "actor.inactive";
    public const string ActorNotFound = "actor.not-found";
    public const string AccessDenied = "access.denied";
    public const string BranchNotFound = "branch.not-found";
    public const string BranchClosed = "branch.closed";

    private readonly ICurrentActor currentActor;
    private readonly IStaffRepository staffRepository;
    private readonly IBranchRepository branchRepository;
    private readonly AuditTrail auditTrail;

    public OperationalAccess(ICurrentActor currentActor, IStaffRepository staffRepository, IBranchRepository branchRepository, AuditTrail auditTrail)
    {
        this.currentActor = currentActor;
        this.staffRepository = staffRepository;
        this.branchRepository = branchRepository;
        this.auditTrail = auditTrail;
    }

    public async Task<ApplicationResult<ActorContext>> CheckAsync(Guid branchId, OperationalCapability capability, string operation, CancellationToken cancellationToken, bool requireOpenBranch = true)
    {
        var actor = currentActor.Actor;
        var authorization = await AuthorizeActorAsync(actor, branchId, capability, operation, cancellationToken);
        if (authorization.Failure is not null) return authorization.Failure;

        if (branchId == Guid.Empty)
            return await DeniedAsync(actor, branchId, operation, BranchNotFound, "The requested branch is unavailable.", cancellationToken);
        var branch = await branchRepository.GetByIdAsync(EntityId.From(branchId), cancellationToken);
        if (branch is null)
            return await DeniedAsync(actor, branchId, operation, BranchNotFound, "The requested branch is unavailable.", cancellationToken);
        if (requireOpenBranch && branch.Value.Status != BranchStatus.Active)
            return await DeniedAsync(actor, branchId, operation, BranchClosed, "The requested branch is not open for operations.", cancellationToken);

        if (branchId != authorization.StaffBranchId)
        {
            if (!authorization.CanAdminister)
                return await DeniedAsync(actor, branchId, operation, AccessDenied, "The actor is not authorized for this branch.", cancellationToken);
            await auditTrail.RecordCrossBranchAccessAsync(actor!, branchId, operation, cancellationToken);
        }

        return ApplicationResults.Success(actor!);
    }

    /// <summary>
    /// Authorizes a list read once for the whole call and returns the branches it may show: all branches for
    /// an administrator, otherwise the actor's own branch. Callers filter rows by the scope instead of
    /// checking each row, so a list writes no audit entry when allowed and a single Denied entry when not.
    /// </summary>
    public async Task<ApplicationResult<BranchScope>> AuthorizeListAsync(OperationalCapability capability, string operation, CancellationToken cancellationToken)
    {
        var actor = currentActor.Actor;
        var authorization = await AuthorizeActorAsync(actor, actor?.AssignedBranchId, capability, operation, cancellationToken);
        if (authorization.Failure is not null) return ApplicationResults.Failure<BranchScope>(authorization.Failure.Errors);
        return ApplicationResults.Success(authorization.CanAdminister ? BranchScope.AllBranches : BranchScope.OnlyBranch(authorization.StaffBranchId));
    }

    public Task AuditAsync(string operation, string targetType, string targetReference, Guid? branchId, string outcome, IReadOnlyDictionary<string, string>? safeMetadata, CancellationToken cancellationToken) =>
        auditTrail.RecordAsync(currentActor.Actor, branchId, operation, targetType, targetReference, outcome, safeMetadata, cancellationToken);

    private async Task<ActorAuthorization> AuthorizeActorAsync(ActorContext? actor, Guid? branchId, OperationalCapability capability, string operation, CancellationToken cancellationToken)
    {
        if (actor is null || actor.StaffId == Guid.Empty || string.IsNullOrWhiteSpace(actor.Role))
            return new(await DeniedAsync(actor, branchId, operation, ActorRequired, "An authenticated staff actor is required.", cancellationToken));

        var staff = await staffRepository.GetByIdAsync(EntityId.From(actor.StaffId), cancellationToken);
        if (staff is null)
            return new(await DeniedAsync(actor, branchId, operation, ActorNotFound, "The authenticated staff actor is unavailable.", cancellationToken));
        if (staff.Value.Status != EmploymentStatus.Active || staff.Value.BranchId.Value != actor.AssignedBranchId)
            return new(await DeniedAsync(actor, branchId, operation, ActorInactive, "The authenticated staff actor is unavailable.", cancellationToken));

        if (!StaffRoleMapping.MatchesStaff(actor.Role, staff.Value.Roles) || !StaffRoleMapping.Allows(actor.Role, capability))
            return new(await DeniedAsync(actor, branchId, operation, AccessDenied, "The actor is not authorized for this operation.", cancellationToken));

        return new(null, staff.Value.BranchId.Value, StaffRoleMapping.IsAdministrator(actor.Role));
    }

    private readonly record struct ActorAuthorization(ApplicationResult<ActorContext>? Failure, Guid StaffBranchId = default, bool CanAdminister = false);

    private async Task<ApplicationResult<ActorContext>> DeniedAsync(ActorContext? actor, Guid? branchId, string operation, string code, string message, CancellationToken cancellationToken)
    {
        await auditTrail.RecordAsync(actor, branchId, operation, "Authorization", operation, "Denied",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Status"] = code }, cancellationToken);
        return Failure<ActorContext>(code, message);
    }

    private static ApplicationResult<T> Failure<T>(string code, string message) => ApplicationResults.Failure<T>([new(code, message)]);
}
