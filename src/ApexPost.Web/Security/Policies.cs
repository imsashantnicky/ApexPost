namespace ApexPost.Web.Security;

/// <summary>Authorization policy names registered in Program and referenced by controllers and views.</summary>
public static class Policies
{
    public const string AdministratorOnly = "AdministratorOnly";
    public const string BranchManagement = "BranchManagement";
    public const string CounterOperations = "CounterOperations";
    public const string InventoryOperations = "InventoryOperations";
    public const string ReportsAccess = "ReportsAccess";
    public const string AuditorReadOnly = "AuditorReadOnly";
    public const string IntegrationCounter = "IntegrationCounter";
}
