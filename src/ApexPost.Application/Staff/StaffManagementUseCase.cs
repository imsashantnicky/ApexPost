using ApexPost.Application.Results;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;

namespace ApexPost.Application.Staff;

public sealed class StaffManagementUseCase
{
    private readonly IStaffRepository repository;
    public StaffManagementUseCase(IStaffRepository repository) => this.repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<ApplicationResult<StaffResponse>> CreateAsync(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.BranchId, out var branchId)) return Invalid<StaffResponse>();
        try
        {
            var staff = new StaffMember(EntityId.New(), EmployeeNumber.From(request.EmployeeNumber), request.Name, branchId, request.Roles);
            if (!await repository.TryAddAsync(staff, cancellationToken)) return Duplicate<StaffResponse>("employeeNumber");
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

    public async Task<ApplicationResult<StaffResponse>> UpdateAsync(UpdateStaffRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.StaffId, out var id) || !TryId(request.BranchId, out var branchId) || request.ExpectedVersion < 1) return Invalid<StaffResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<StaffResponse>();
        if (existing.Version != request.ExpectedVersion) return Conflict<StaffResponse>();
        try
        {
            existing.Value.Rename(request.Name);
            existing.Value.AssignToBranch(branchId);
            existing.Value.ChangeRoles(request.Roles);
            return await repository.TryUpdateAsync(existing.Value, request.ExpectedVersion, cancellationToken)
                ? ApplicationResults.Success(ToResponse(existing.Value, request.ExpectedVersion + 1)) : Conflict<StaffResponse>();
        }
        catch (DomainException exception) { return DomainFailure<StaffResponse>(exception); }
    }

    public async Task<ApplicationResult<StaffResponse>> DeactivateAsync(Guid staffId, long expectedVersion, CancellationToken cancellationToken)
    {
        if (!TryId(staffId, out var id) || expectedVersion < 1) return Invalid<StaffResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<StaffResponse>();
        if (existing.Version != expectedVersion) return Conflict<StaffResponse>();
        try
        {
            existing.Value.Suspend();
            return await repository.TryUpdateAsync(existing.Value, expectedVersion, cancellationToken)
                ? ApplicationResults.Success(ToResponse(existing.Value, expectedVersion + 1)) : Conflict<StaffResponse>();
        }
        catch (DomainException exception) { return DomainFailure<StaffResponse>(exception); }
    }

    private static StaffResponse ToResponse(StaffMember staff, long version) => new(staff.Id.Value, version, staff.EmployeeNumber.Value, staff.Name, staff.BranchId.Value, staff.Roles, staff.Status);
    private static bool TryId(Guid value, out EntityId id) { id = value == Guid.Empty ? null! : EntityId.From(value); return value != Guid.Empty; }
    private static ApplicationResult<T> Invalid<T>() => ApplicationResults.Failure<T>([new("request.invalid", "The request is invalid.")]);
    private static ApplicationResult<T> NotFound<T>() => ApplicationResults.Failure<T>([new("resource.not-found", "The requested resource was not found.")]);
    private static ApplicationResult<T> Duplicate<T>(string field) => ApplicationResults.Failure<T>([new("identifier.duplicate", "The business identifier is already in use.", field)]);
    private static ApplicationResult<T> Conflict<T>() => ApplicationResults.Failure<T>([new("concurrency.conflict", "The resource changed before it could be saved. Reload and try again.")]);
    private static ApplicationResult<T> DomainFailure<T>(DomainException exception) => ApplicationResults.Failure<T>([new("domain.rule", exception.Message)]);
}
