using ApexPost.Domain.Staff;

namespace ApexPost.Application.Security;

/// <summary>Account role names used in claims, policies and audit records.</summary>
public static class Roles
{
    public const string Administrator = "Administrator";
    public const string BranchManager = "BranchManager";
    public const string CounterStaff = "CounterStaff";
    public const string InventoryStaff = "InventoryStaff";
    public const string Auditor = "Auditor";

    public static IReadOnlyList<string> All { get; } = [Administrator, BranchManager, CounterStaff, InventoryStaff, Auditor];
}

/// <summary>
/// The single mapping between account roles, legacy role names, staff assignments and operational capabilities.
/// </summary>
public static class StaffRoleMapping
{
    /// <summary>Returns the canonical account role for a current or legacy name, or the trimmed input when unknown.</summary>
    public static string Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return string.Empty;
        var trimmed = role.Trim();
        return trimmed switch
        {
            "CounterClerk" => Roles.CounterStaff,
            "InventoryClerk" => Roles.InventoryStaff,
            "OperationsManager" => Roles.BranchManager,
            _ => Roles.All.FirstOrDefault(known => string.Equals(known, trimmed, StringComparison.OrdinalIgnoreCase)) ?? trimmed
        };
    }

    public static bool IsSupported(string? role) => Roles.All.Contains(Normalize(role), StringComparer.Ordinal);

    public static bool IsReadOnly(string? role) => Normalize(role) == Roles.Auditor;

    public static bool IsAdministrator(string? role) => Normalize(role) == Roles.Administrator;

    /// <summary>Whether a staff member's assignments allow them to act in this account role.</summary>
    public static bool MatchesStaff(string? role, IReadOnlyCollection<StaffRole> staffRoles)
    {
        ArgumentNullException.ThrowIfNull(staffRoles);
        return Normalize(role) switch
        {
            Roles.Administrator or Roles.Auditor => true,
            Roles.BranchManager => staffRoles.Contains(StaffRole.BranchManager),
            Roles.CounterStaff => staffRoles.Contains(StaffRole.CounterClerk),
            Roles.InventoryStaff => staffRoles.Contains(StaffRole.InventoryClerk),
            _ => false
        };
    }

    /// <summary>Whether the role may perform operations of this kind. Auditors are read-only and never may.</summary>
    public static bool Allows(string? role, OperationalCapability capability) => (Normalize(role), capability) switch
    {
        (Roles.Administrator or Roles.BranchManager, _) => true,
        (Roles.InventoryStaff, OperationalCapability.Inventory) => true,
        (Roles.CounterStaff, OperationalCapability.Counter) => true,
        _ => false
    };

    public static bool CanReadReports(string? role) => Normalize(role) is Roles.Administrator or Roles.BranchManager or Roles.Auditor;
}
