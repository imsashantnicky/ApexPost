using ApexPost.Domain.Common;

namespace ApexPost.Domain.Staff;

public sealed class StaffMember
{
    private readonly HashSet<StaffRole> roles;

    public StaffMember(
        EntityId id,
        EmployeeNumber employeeNumber,
        string name,
        EntityId branchId,
        IEnumerable<StaffRole> roles)
    {
        Id = id ?? throw new DomainException("Staff identity is required.");
        EmployeeNumber = employeeNumber ?? throw new DomainException("Employee number is required.");
        Name = RequiredName(name);
        BranchId = branchId ?? throw new DomainException("Branch assignment is required.");
        this.roles = ValidRoles(roles);
        Status = EmploymentStatus.Active;
    }

    public EntityId Id { get; }

    public EmployeeNumber EmployeeNumber { get; }

    public string Name { get; private set; }

    public EntityId BranchId { get; private set; }

    public EmploymentStatus Status { get; private set; }

    public IReadOnlyCollection<StaffRole> Roles => roles.ToArray();

    public void Rename(string name)
    {
        EnsureNotTerminated();
        Name = RequiredName(name);
    }

    public void AssignToBranch(EntityId branchId)
    {
        EnsureNotTerminated();
        BranchId = branchId ?? throw new DomainException("Branch assignment is required.");
    }

    public void ChangeRoles(IEnumerable<StaffRole> newRoles)
    {
        EnsureNotTerminated();
        var validatedRoles = ValidRoles(newRoles);
        roles.Clear();
        roles.UnionWith(validatedRoles);
    }

    public void Suspend()
    {
        if (Status != EmploymentStatus.Active)
        {
            throw new DomainException("Only active staff can be suspended.");
        }

        Status = EmploymentStatus.Suspended;
    }

    public void Activate()
    {
        if (Status != EmploymentStatus.Suspended)
        {
            throw new DomainException("Only suspended staff can be activated.");
        }

        Status = EmploymentStatus.Active;
    }

    public void Terminate()
    {
        EnsureNotTerminated();
        Status = EmploymentStatus.Terminated;
    }

    public static StaffMember Restore(EntityId id, EmployeeNumber employeeNumber, string name, EntityId branchId, IEnumerable<StaffRole> roles, EmploymentStatus status)
    {
        var staffMember = new StaffMember(id, employeeNumber, name, branchId, roles);
        if (status == EmploymentStatus.Suspended) staffMember.Suspend();
        else if (status == EmploymentStatus.Terminated) staffMember.Terminate();
        else if (status != EmploymentStatus.Active) throw new DomainException("Employment status must be recognized.");
        return staffMember;
    }

    private static string RequiredName(string name)
    {
        var normalizedName = name?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedName) || normalizedName.Length > 150)
        {
            throw new DomainException("Staff name is required and must not exceed 150 characters.");
        }

        return normalizedName;
    }

    private static HashSet<StaffRole> ValidRoles(IEnumerable<StaffRole> values)
    {
        var roleValues = values?.ToArray() ?? throw new DomainException("At least one staff role is required.");

        if (roleValues.Length == 0)
        {
            throw new DomainException("At least one staff role is required.");
        }

        var uniqueRoles = new HashSet<StaffRole>(roleValues);

        if (uniqueRoles.Count != roleValues.Length)
        {
            throw new DomainException("Staff roles must not contain duplicates.");
        }

        if (uniqueRoles.Any(role => !Enum.IsDefined(role)))
        {
            throw new DomainException("Staff roles must be recognized roles.");
        }

        return uniqueRoles;
    }

    private void EnsureNotTerminated()
    {
        if (Status == EmploymentStatus.Terminated)
        {
            throw new DomainException("A terminated staff member cannot be changed.");
        }
    }
}
