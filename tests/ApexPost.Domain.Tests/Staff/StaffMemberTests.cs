using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;

namespace ApexPost.Domain.Tests.Staff;

public class StaffMemberTests
{
    [Fact]
    public void ConstructorWithValidValuesCreatesActiveStaffMember()
    {
        var branchId = EntityId.New();
        var staffMember = new StaffMember(
            EntityId.New(),
            EmployeeNumber.From(" emp-0001 "),
            "  Anika Sharma  ",
            branchId,
            [StaffRole.CounterClerk]);

        Assert.Equal("EMP-0001", staffMember.EmployeeNumber.Value);
        Assert.Equal("Anika Sharma", staffMember.Name);
        Assert.Equal(branchId, staffMember.BranchId);
        Assert.Equal(EmploymentStatus.Active, staffMember.Status);
        Assert.Equal([StaffRole.CounterClerk], staffMember.Roles);
    }

    [Theory]
    [InlineData("")]
    [InlineData("EMP-123")]
    [InlineData("E-0001")]
    [InlineData("EMP-12A4")]
    public void EmployeeNumberWithInvalidFormatThrowsDomainException(string value)
    {
        Assert.Throws<DomainException>(() => EmployeeNumber.From(value));
    }

    [Fact]
    public void ConstructorWithBlankNameThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new StaffMember(
            EntityId.New(),
            EmployeeNumber.From("EMP-0001"),
            " ",
            EntityId.New(),
            [StaffRole.CounterClerk]));
    }

    [Fact]
    public void ConstructorWithNoRolesThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new StaffMember(
            EntityId.New(),
            EmployeeNumber.From("EMP-0001"),
            "Anika Sharma",
            EntityId.New(),
            []));
    }

    [Fact]
    public void ConstructorWithDuplicateRolesThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new StaffMember(
            EntityId.New(),
            EmployeeNumber.From("EMP-0001"),
            "Anika Sharma",
            EntityId.New(),
            [StaffRole.CounterClerk, StaffRole.CounterClerk]));
    }

    [Fact]
    public void SuspendThenActivateChangesEmploymentStatus()
    {
        var staffMember = CreateStaffMember();

        staffMember.Suspend();
        staffMember.Activate();

        Assert.Equal(EmploymentStatus.Active, staffMember.Status);
    }

    [Fact]
    public void SuspendSuspendedStaffMemberThrowsDomainException()
    {
        var staffMember = CreateStaffMember();
        staffMember.Suspend();

        Assert.Throws<DomainException>(staffMember.Suspend);
    }

    [Fact]
    public void TerminatedStaffMemberCannotBeReassignedOrHaveRolesChanged()
    {
        var staffMember = CreateStaffMember();
        staffMember.Terminate();

        Assert.Equal(EmploymentStatus.Terminated, staffMember.Status);
        Assert.Throws<DomainException>(() => staffMember.AssignToBranch(EntityId.New()));
        Assert.Throws<DomainException>(() => staffMember.ChangeRoles([StaffRole.BranchManager]));
    }

    [Fact]
    public void ChangeRolesReplacesExistingDistinctRoles()
    {
        var staffMember = CreateStaffMember();

        staffMember.ChangeRoles([StaffRole.BranchManager, StaffRole.InventoryClerk]);

        Assert.Equal([StaffRole.BranchManager, StaffRole.InventoryClerk], staffMember.Roles.Order());
    }

    [Fact]
    public void ChangeRolesWithInvalidInputPreservesExistingRoles()
    {
        var staffMember = CreateStaffMember();

        Assert.Throws<DomainException>(() => staffMember.ChangeRoles([]));

        Assert.Equal([StaffRole.CounterClerk], staffMember.Roles);
    }

    private static StaffMember CreateStaffMember()
    {
        return new StaffMember(
            EntityId.New(),
            EmployeeNumber.From("EMP-0001"),
            "Anika Sharma",
            EntityId.New(),
            [StaffRole.CounterClerk]);
    }
}
