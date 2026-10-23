using ApexPost.Domain.Staff;

namespace ApexPost.Application.Staff;

public sealed record CreateStaffRequest(string EmployeeNumber, string Name, Guid BranchId, IReadOnlyCollection<StaffRole> Roles);
public sealed record UpdateStaffRequest(Guid StaffId, long ExpectedVersion, string Name, Guid BranchId, IReadOnlyCollection<StaffRole> Roles);
public sealed record StaffResponse(Guid Id, long Version, string EmployeeNumber, string Name, Guid BranchId, IReadOnlyCollection<StaffRole> Roles, EmploymentStatus Status);
