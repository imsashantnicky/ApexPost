using ApexPost.Infrastructure.Persistence.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;

namespace ApexPost.Web.Services;

public sealed class LocalAccountService
{
    public const string AdministratorRole = "Administrator";
    public const string BranchManagerRole = "BranchManager";
    public const string CounterStaffRole = "CounterStaff";
    public const string InventoryStaffRole = "InventoryStaff";
    public const string AuditorRole = "Auditor";

    private static readonly SemaphoreSlim BootstrapLock = new(1, 1);
    private static readonly Regex UserNamePattern = new("^[A-Za-z0-9._-]{3,80}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        AdministratorRole,
        BranchManagerRole,
        CounterStaffRole,
        InventoryStaffRole,
        AuditorRole,
        "CounterClerk",
        "InventoryClerk",
        "OperationsManager"
    };

    private readonly JsonCollectionStore<AccountRecord> store;
    private readonly PasswordHasher<AccountRecord> hasher = new();

    public LocalAccountService(JsonDataOptions options) => store = new JsonCollectionStore<AccountRecord>(options.DataDirectory, "accounts.json");

    public static IReadOnlyList<string> SupportedRoles { get; } = [AdministratorRole, BranchManagerRole, CounterStaffRole, InventoryStaffRole, AuditorRole];

    public static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return string.Empty;

        var normalized = role.Trim();
        return normalized switch
        {
            "CounterClerk" => CounterStaffRole,
            "InventoryClerk" => InventoryStaffRole,
            "OperationsManager" => BranchManagerRole,
            _ => normalized
        };
    }

    public static bool IsSupportedRole(string? role) => !string.IsNullOrWhiteSpace(role) && AllowedRoles.Contains(NormalizeRole(role));

    public static bool IsReadOnlyRole(string? role) => string.Equals(NormalizeRole(role), AuditorRole, StringComparison.OrdinalIgnoreCase);

    public static bool CanAccessBranch(string? role, string? assignedBranchId, string? requestedBranchId)
    {
        var normalizedRole = NormalizeRole(role);
        if (string.Equals(normalizedRole, AdministratorRole, StringComparison.OrdinalIgnoreCase) || string.Equals(normalizedRole, AuditorRole, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(requestedBranchId))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(assignedBranchId))
        {
            return false;
        }

        return string.Equals(assignedBranchId.Trim(), requestedBranchId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static string GetDashboardAction(string? role)
    {
        return NormalizeRole(role) switch
        {
            AdministratorRole => "Admin",
            BranchManagerRole => "Branch",
            CounterStaffRole => "Counter",
            InventoryStaffRole => "Inventory",
            AuditorRole => "Auditor",
            _ => "Index"
        };
    }

    public async Task<bool> HasAccountsAsync(CancellationToken token) => (await store.ReadAllAsync(token)).Count > 0;

    public async Task<bool> HasAccountAsync(string userName, CancellationToken token)
    {
        var normalized = NormalizeUserName(userName);
        if (!IsValidUserName(normalized)) return false;
        var accounts = await store.ReadAllAsync(token);
        return accounts.ContainsKey(normalized.ToLowerInvariant());
    }

    public async Task<bool> BootstrapAsync(string userName, string password, CancellationToken token)
    {
        await BootstrapLock.WaitAsync(token);
        try
        {
            if (!IsValidUserName(userName) || password is null || password.Length < 12 || await HasAccountsAsync(token)) return false;
            return await CreateAsync(userName, password, AdministratorRole, token);
        }
        finally
        {
            BootstrapLock.Release();
        }
    }

    public async Task<bool> CreateAsync(string userName, string password, string role, CancellationToken token) => await CreateAsync(userName, password, role, null, token);

    public async Task<bool> CreateAsync(string userName, string password, string role, string? assignedBranchId, CancellationToken token)
    {
        var normalizedUserName = NormalizeUserName(userName);
        var normalizedRole = NormalizeRole(role);
        if (!IsValidUserName(normalizedUserName) || password is null || password.Length < 12 || !IsSupportedRole(normalizedRole)) return false;

        var record = new AccountRecord(normalizedUserName, string.Empty, normalizedRole, assignedBranchId);
        record = record with { PasswordHash = hasher.HashPassword(record, password) };
        return await store.TryAddAsync(normalizedUserName.ToLowerInvariant(), record, token);
    }

    public async Task<AccountRecord?> ValidateAsync(string userName, string password, CancellationToken token)
    {
        var normalized = NormalizeUserName(userName);
        var accounts = await store.ReadAllAsync(token);
        if (!accounts.TryGetValue(normalized.ToLowerInvariant(), out var account)) return null;
        if (password is null) return null;
        return hasher.VerifyHashedPassword(account, account.PasswordHash, password) is not PasswordVerificationResult.Failed ? account : null;
    }

    public async Task InitializeDevelopmentAccountsAsync(IWebHostEnvironment environment, IConfiguration configuration, CancellationToken token)
    {
        if (!configuration.GetValue<bool>("ApexPost:DevelopmentAccounts:Enabled"))
        {
            return;
        }

        if (!environment.IsDevelopment())
        {
            return;
        }

        var section = configuration.GetSection("ApexPost:DevelopmentAccounts");
        foreach (var role in SupportedRoles)
        {
            var userName = section[$"{role}:UserName"] ?? section[$"{role}:Username"] ?? Environment.GetEnvironmentVariable($"ApexPost__DevelopmentAccounts__{role}__UserName") ?? Environment.GetEnvironmentVariable($"ApexPost__DevelopmentAccounts__{role}__Username");
            var password = section[$"{role}:Password"] ?? Environment.GetEnvironmentVariable($"ApexPost__DevelopmentAccounts__{role}__Password");
            var assignedBranchId = section[$"{role}:BranchId"] ?? Environment.GetEnvironmentVariable($"ApexPost__DevelopmentAccounts__{role}__BranchId");

            var hasAnySetting = !string.IsNullOrWhiteSpace(userName) || !string.IsNullOrWhiteSpace(password) || !string.IsNullOrWhiteSpace(assignedBranchId);
            if (!hasAnySetting)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException($"Development account configuration for role '{role}' is incomplete. Set ApexPost__DevelopmentAccounts__{role}__UserName and ApexPost__DevelopmentAccounts__{role}__Password or the matching user-secrets values.");
            }

            if (await HasAccountAsync(userName, token))
            {
                continue;
            }

            var created = await CreateAsync(userName, password, role, assignedBranchId, token);
            if (!created)
            {
                throw new InvalidOperationException($"Development account '{userName}' for role '{role}' could not be created. Check for a duplicate or invalid username.");
            }
        }
    }

    private static string NormalizeUserName(string? userName) => userName?.Trim() ?? string.Empty;
    private static bool IsValidUserName(string? userName) => userName is not null && UserNamePattern.IsMatch(userName.Trim());
}

public sealed record AccountRecord(string UserName, string PasswordHash, string Role, string? AssignedBranchId = null);
