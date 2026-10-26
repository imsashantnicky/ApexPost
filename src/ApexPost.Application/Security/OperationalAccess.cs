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

public interface IAccessAuditWriter
{
    Task WriteAdministratorCrossBranchAccessAsync(ActorContext actor, Guid branchId, string operation, CancellationToken cancellationToken);
    Task WriteOperationAsync(ActorContext? actor, Guid? branchId, string operation, string targetType, string targetReference, string outcome, IReadOnlyDictionary<string, string>? safeMetadata, CancellationToken cancellationToken);
}

public enum OperationalCapability { Inventory, Counter, BranchManager }

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
    private readonly IAccessAuditWriter auditWriter;

    public OperationalAccess(ICurrentActor currentActor, IStaffRepository staffRepository, IBranchRepository branchRepository, IAccessAuditWriter auditWriter)
    {
        this.currentActor = currentActor;
        this.staffRepository = staffRepository;
        this.branchRepository = branchRepository;
        this.auditWriter = auditWriter;
    }

    public async Task<ApplicationResult<ActorContext>> CheckAsync(Guid branchId, OperationalCapability capability, string operation, CancellationToken cancellationToken, bool requireOpenBranch = true)
    {
        var actor = currentActor.Actor;
        if (actor is null || actor.StaffId == Guid.Empty || string.IsNullOrWhiteSpace(actor.Role))
            return await DeniedAsync(actor, branchId, operation, ActorRequired, "An authenticated staff actor is required.", cancellationToken);

        var staff = await staffRepository.GetByIdAsync(EntityId.From(actor.StaffId), cancellationToken);
        if (staff is null)
            return await DeniedAsync(actor, branchId, operation, ActorNotFound, "The authenticated staff actor is unavailable.", cancellationToken);
        if (staff.Value.Status != EmploymentStatus.Active || staff.Value.BranchId.Value != actor.AssignedBranchId)
            return await DeniedAsync(actor, branchId, operation, ActorInactive, "The authenticated staff actor is unavailable.", cancellationToken);

        var role = LocalRole(actor.Role);
        if (!RoleMatchesStaff(role, staff.Value.Roles))
            return await DeniedAsync(actor, branchId, operation, AccessDenied, "The actor is not authorized for this operation.", cancellationToken);

        var canAdminister = role == "Administrator";
        var permittedRole = capability switch
        {
            OperationalCapability.Inventory => role is "Administrator" or "BranchManager" or "InventoryStaff",
            OperationalCapability.Counter => role is "Administrator" or "BranchManager" or "CounterStaff",
            OperationalCapability.BranchManager => role is "Administrator" or "BranchManager",
            _ => false
        };
        if (!permittedRole || role == "Auditor")
            return await DeniedAsync(actor, branchId, operation, AccessDenied, "The actor is not authorized for this operation.", cancellationToken);

        if (branchId == Guid.Empty)
            return await DeniedAsync(actor, branchId, operation, BranchNotFound, "The requested branch is unavailable.", cancellationToken);
        var branch = await branchRepository.GetByIdAsync(EntityId.From(branchId), cancellationToken);
        if (branch is null)
            return await DeniedAsync(actor, branchId, operation, BranchNotFound, "The requested branch is unavailable.", cancellationToken);
        if (requireOpenBranch && branch.Value.Status != BranchStatus.Active)
            return await DeniedAsync(actor, branchId, operation, BranchClosed, "The requested branch is not open for operations.", cancellationToken);

        if (branchId != staff.Value.BranchId.Value)
        {
            if (!canAdminister)
                return await DeniedAsync(actor, branchId, operation, AccessDenied, "The actor is not authorized for this branch.", cancellationToken);
            await auditWriter.WriteAdministratorCrossBranchAccessAsync(actor, branchId, operation, cancellationToken);
        }

        return ApplicationResults.Success(actor);
    }

    public Task AuditAsync(string operation, string targetType, string targetReference, Guid? branchId, string outcome, IReadOnlyDictionary<string, string>? safeMetadata, CancellationToken cancellationToken) =>
        auditWriter.WriteOperationAsync(currentActor.Actor, branchId, operation, targetType, targetReference, outcome, safeMetadata, cancellationToken);

    public Task<ApplicationResult<ActorContext>> CheckCurrentBranchAsync(OperationalCapability capability, string operation, CancellationToken cancellationToken, bool requireOpenBranch = false)
    {
        var actor = currentActor.Actor;
        if (actor is null || actor.AssignedBranchId is not Guid branchId)
            return DeniedAsync(actor, null, operation, ActorRequired, "An authenticated staff actor is required.", cancellationToken);
        return CheckAsync(branchId, capability, operation, cancellationToken, requireOpenBranch);
    }

    private async Task<ApplicationResult<ActorContext>> DeniedAsync(ActorContext? actor, Guid? branchId, string operation, string code, string message, CancellationToken cancellationToken)
    {
        await auditWriter.WriteOperationAsync(actor, branchId, operation, "Authorization", operation, "Denied",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Status"] = code }, cancellationToken);
        return Failure<ActorContext>(code, message);
    }

    private static bool RoleMatchesStaff(string role, IReadOnlyCollection<StaffRole> staffRoles) => role switch
    {
        "Administrator" => true,
        "BranchManager" => staffRoles.Contains(StaffRole.BranchManager),
        "CounterStaff" => staffRoles.Contains(StaffRole.CounterClerk),
        "InventoryStaff" => staffRoles.Contains(StaffRole.InventoryClerk),
        _ => false
    };

    private static string LocalRole(string role) => role switch
    {
        "OperationsManager" => "BranchManager",
        "CounterClerk" => "CounterStaff",
        "InventoryClerk" => "InventoryStaff",
        _ => role
    };

    private static ApplicationResult<T> Failure<T>(string code, string message) => ApplicationResults.Failure<T>([new(code, message)]);
}
