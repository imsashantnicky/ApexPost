using ApexPost.Application.Security;
using ApexPost.Domain.Staff;

namespace ApexPost.Application.Tests;

public sealed class StaffRoleMappingTests
{
    [Theory]
    [InlineData("CounterClerk", Roles.CounterStaff)]
    [InlineData("InventoryClerk", Roles.InventoryStaff)]
    [InlineData("OperationsManager", Roles.BranchManager)]
    [InlineData(" administrator ", Roles.Administrator)]
    [InlineData("AUDITOR", Roles.Auditor)]
    public void Normalize_MapsLegacyAndMixedCaseNamesToCanonicalRoles(string input, string expected)
    {
        Assert.Equal(expected, StaffRoleMapping.Normalize(input));
        Assert.True(StaffRoleMapping.IsSupported(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SuperUser")]
    public void IsSupported_RejectsUnknownRoles(string? role) => Assert.False(StaffRoleMapping.IsSupported(role));

    [Theory]
    [InlineData(Roles.Administrator, OperationalCapability.Inventory, true)]
    [InlineData(Roles.Administrator, OperationalCapability.BranchManager, true)]
    [InlineData(Roles.BranchManager, OperationalCapability.Counter, true)]
    [InlineData(Roles.CounterStaff, OperationalCapability.Counter, true)]
    [InlineData(Roles.CounterStaff, OperationalCapability.Inventory, false)]
    [InlineData(Roles.CounterStaff, OperationalCapability.BranchManager, false)]
    [InlineData(Roles.InventoryStaff, OperationalCapability.Inventory, true)]
    [InlineData(Roles.InventoryStaff, OperationalCapability.Counter, false)]
    [InlineData(Roles.Auditor, OperationalCapability.Inventory, false)]
    [InlineData(Roles.Auditor, OperationalCapability.Counter, false)]
    public void Allows_FollowsTheCapabilityMatrix(string role, OperationalCapability capability, bool expected) =>
        Assert.Equal(expected, StaffRoleMapping.Allows(role, capability));

    [Theory]
    [InlineData(Roles.BranchManager, StaffRole.BranchManager, true)]
    [InlineData(Roles.BranchManager, StaffRole.CounterClerk, false)]
    [InlineData(Roles.CounterStaff, StaffRole.CounterClerk, true)]
    [InlineData(Roles.InventoryStaff, StaffRole.InventoryClerk, true)]
    [InlineData(Roles.InventoryStaff, StaffRole.CounterClerk, false)]
    [InlineData(Roles.Administrator, StaffRole.CounterClerk, true)]
    [InlineData(Roles.Auditor, StaffRole.CounterClerk, true)]
    public void MatchesStaff_RequiresACompatibleAssignment(string role, StaffRole assignment, bool expected) =>
        Assert.Equal(expected, StaffRoleMapping.MatchesStaff(role, [assignment]));

    [Fact]
    public void ReportsAreReadableOnlyByAdministratorsManagersAndAuditors()
    {
        Assert.Equal([Roles.Administrator, Roles.BranchManager, Roles.Auditor], Roles.All.Where(StaffRoleMapping.CanReadReports));
        Assert.True(StaffRoleMapping.IsReadOnly(Roles.Auditor));
    }
}
