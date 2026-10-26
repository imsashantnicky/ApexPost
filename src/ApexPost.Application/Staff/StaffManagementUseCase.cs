using ApexPost.Application.Results;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;
using ApexPost.Application.Security;

namespace ApexPost.Application.Staff;

public sealed class StaffManagementUseCase
{
    private readonly IStaffRepository repository;
    private readonly ICurrentActor currentActor;
    private readonly IAccessAuditWriter auditWriter;
    public StaffManagementUseCase(IStaffRepository repository, ICurrentActor currentActor, IAccessAuditWriter auditWriter)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.currentActor = currentActor ?? throw new ArgumentNullException(nameof(currentActor));
        this.auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
    }

    public async Task<ApplicationResult<StaffResponse>> CreateAsync(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.BranchId, out var branchId)) return Invalid<StaffResponse>();
        try
        {
            var staff = new StaffMember(EntityId.New(), EmployeeNumber.From(request.EmployeeNumber), request.Name, branchId, request.Roles);
            await AuditAsync("Staff.Create", staff, "Started", cancellationToken);
            if (!await repository.TryAddAsync(staff, cancellationToken)) { await AuditAsync("Staff.Create", staff, "Failed", cancellationToken); return Duplicate<StaffResponse>("employeeNumber"); }
            await AuditAsync("Staff.Create", staff, "Succeeded", cancellationToken);
            return ApplicationResults.Success(ToResponse(staff, 1));
        }
        catch (DomainException exception) { return DomainFailure<StaffResponse>(exception); }
    }

    public async Task<ApplicationResult<StaffResponse>> GetAsync(Guid staffId, CancellationToken cancellationToken)
    {
        if (!TryId(staffId, out var id)) return Invalid<StaffResponse>();
        var result = await repository.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound<StaffResponse>() : ApplicationResults.Success(ToResponse(result.Value, result.Version));
    }

    public async Task<IReadOnlyList<StaffResponse>> ListAsync(CancellationToken cancellationToken)
    {
        return (await repository.ListAsync(cancellationToken)).Select(item => ToResponse(item.Value, item.Version)).OrderBy(item => item.EmployeeNumber).ToArray();
    }

    public async Task<ApplicationResult<StaffResponse>> UpdateAsync(UpdateStaffRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.StaffId, out var id) || !TryId(request.BranchId, out var branchId) || request.ExpectedVersion < 1) return Invalid<StaffResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<StaffResponse>();
        if (existing.Version != request.ExpectedVersion) { await AuditAsync("Staff.Update", existing.Value, "Failed", cancellationToken); return Conflict<StaffResponse>(); }
        try
        {
            existing.Value.Rename(request.Name);
            existing.Value.AssignToBranch(branchId);
            existing.Value.ChangeRoles(request.Roles);
            await AuditAsync("Staff.Update", existing.Value, "Started", cancellationToken);
            if (!await repository.TryUpdateAsync(existing.Value, request.ExpectedVersion, cancellationToken)) return Conflict<StaffResponse>();
            await AuditAsync("Staff.Update", existing.Value, "Succeeded", cancellationToken);
            return ApplicationResults.Success(ToResponse(existing.Value, request.ExpectedVersion + 1));
        }
        catch (DomainException exception) { return DomainFailure<StaffResponse>(exception); }
    }

    public async Task<ApplicationResult<StaffResponse>> DeactivateAsync(Guid staffId, long expectedVersion, CancellationToken cancellationToken)
    {
        if (!TryId(staffId, out var id) || expectedVersion < 1) return Invalid<StaffResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<StaffResponse>();
        if (existing.Version != expectedVersion) { await AuditAsync("Staff.Deactivate", existing.Value, "Failed", cancellationToken); return Conflict<StaffResponse>(); }
        try
        {
            existing.Value.Suspend();
            await AuditAsync("Staff.Deactivate", existing.Value, "Started", cancellationToken);
            if (!await repository.TryUpdateAsync(existing.Value, expectedVersion, cancellationToken)) return Conflict<StaffResponse>();
            await AuditAsync("Staff.Deactivate", existing.Value, "Succeeded", cancellationToken);
            return ApplicationResults.Success(ToResponse(existing.Value, expectedVersion + 1));
        }
        catch (DomainException exception) { return DomainFailure<StaffResponse>(exception); }
    }

    private static StaffResponse ToResponse(StaffMember staff, long version) => new(staff.Id.Value, version, staff.EmployeeNumber.Value, staff.Name, staff.BranchId.Value, staff.Roles, staff.Status);
    private Task AuditAsync(string operation, StaffMember staff, string outcome, CancellationToken token) => auditWriter.WriteOperationAsync(currentActor.Actor, staff.BranchId.Value, operation, "Staff", staff.Id.Value.ToString("D"), outcome, new Dictionary<string, string> { ["Status"] = staff.Status.ToString(), ["StaffRoles"] = string.Join(',', staff.Roles.Order()) }, token);
    private static bool TryId(Guid value, out EntityId id) { id = value == Guid.Empty ? null! : EntityId.From(value); return value != Guid.Empty; }
    private static ApplicationResult<T> Invalid<T>() => ApplicationResults.Failure<T>([new("request.invalid", "The request is invalid.")]);
    private static ApplicationResult<T> NotFound<T>() => ApplicationResults.Failure<T>([new("resource.not-found", "The requested resource was not found.")]);
    private static ApplicationResult<T> Duplicate<T>(string field) => ApplicationResults.Failure<T>([new("identifier.duplicate", "The business identifier is already in use.", field)]);
    private static ApplicationResult<T> Conflict<T>() => ApplicationResults.Failure<T>([new("concurrency.conflict", "The resource changed before it could be saved. Reload and try again.")]);
    private static ApplicationResult<T> DomainFailure<T>(DomainException exception) => ApplicationResults.Failure<T>([new("domain.rule", exception.Message)]);
}
