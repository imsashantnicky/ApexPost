using ApexPost.Infrastructure.Persistence.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using ApexPost.Application.Security;
using ApexPost.Application.Staff;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;
using System.Text.RegularExpressions;

namespace ApexPost.Web.Services;

public sealed class LocalAccountService
{
    private static readonly SemaphoreSlim BootstrapLock = new(1, 1);
    private static readonly Regex UserNamePattern = new("^[A-Za-z0-9._-]{3,80}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly JsonCollectionStore<AccountRecord> store;
    private readonly PasswordHasher<AccountRecord> hasher = new();

    public LocalAccountService(JsonDataOptions options) => store = new JsonCollectionStore<AccountRecord>(options.DataDirectory, "accounts.json");

    public static string GetDashboardAction(string? role)
    {
        return StaffRoleMapping.Normalize(role) switch
        {
            Roles.Administrator => "Admin",
            Roles.BranchManager => "Branch",
            Roles.CounterStaff => "Counter",
            Roles.InventoryStaff => "Inventory",
            Roles.Auditor => "Auditor",
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

    public async Task<bool> BootstrapAsync(string userName, string password, Guid staffId, Guid branchId, CancellationToken token)
    {
        await BootstrapLock.WaitAsync(token);
        try
        {
            if (!IsValidUserName(userName) || password is null || password.Length < 12 || staffId == Guid.Empty || branchId == Guid.Empty || await HasAccountsAsync(token)) return false;
            return await CreateAsync(userName, password, Roles.Administrator, staffId, branchId, token);
        }
        finally
        {
            BootstrapLock.Release();
        }
    }

    public async Task<bool> CreateAsync(string userName, string password, string role, Guid staffId, Guid branchId, CancellationToken token)
    {
        var normalizedUserName = NormalizeUserName(userName);
        var normalizedRole = StaffRoleMapping.Normalize(role);
        if (!IsValidUserName(normalizedUserName) || password is null || password.Length < 12 || !StaffRoleMapping.IsSupported(normalizedRole) || staffId == Guid.Empty || branchId == Guid.Empty) return false;
        var record = new AccountRecord(normalizedUserName, string.Empty, normalizedRole, branchId.ToString("D"), staffId);
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

    public async Task InitializeDevelopmentAccountsAsync(IWebHostEnvironment environment, IConfiguration configuration, IStaffRepository staffRepository, CancellationToken token)
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
        foreach (var role in Roles.All)
        {
            var userName = section[$"{role}:UserName"] ?? section[$"{role}:Username"] ?? Environment.GetEnvironmentVariable($"ApexPost__DevelopmentAccounts__{role}__UserName") ?? Environment.GetEnvironmentVariable($"ApexPost__DevelopmentAccounts__{role}__Username");
            var password = section[$"{role}:Password"] ?? Environment.GetEnvironmentVariable($"ApexPost__DevelopmentAccounts__{role}__Password");
            var staffIdValue = section[$"{role}:StaffId"] ?? Environment.GetEnvironmentVariable($"ApexPost__DevelopmentAccounts__{role}__StaffId");

            var hasAnySetting = !string.IsNullOrWhiteSpace(userName) || !string.IsNullOrWhiteSpace(password) || !string.IsNullOrWhiteSpace(staffIdValue);
            if (!hasAnySetting)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password) || !Guid.TryParse(staffIdValue, out var staffId) || staffId == Guid.Empty)
            {
                throw new InvalidOperationException($"Development account configuration for role '{role}' is incomplete. Set its UserName, Password and StaffId values in local secrets or environment configuration.");
            }

            if (await HasAccountAsync(userName, token))
            {
                continue;
            }

            var staff = await staffRepository.GetByIdAsync(EntityId.From(staffId), token);
            if (staff is null || staff.Value.Status != EmploymentStatus.Active || !StaffRoleMapping.MatchesStaff(role, staff.Value.Roles))
                throw new InvalidOperationException($"Development account configuration for role '{role}' must reference active staff with a compatible assignment.");
            var created = await CreateAsync(userName, password, role, staff.Value.Id.Value, staff.Value.BranchId.Value, token);
            if (!created)
            {
                throw new InvalidOperationException($"Development account '{userName}' for role '{role}' could not be created. Check for a duplicate or invalid username.");
            }
        }
    }

    private static string NormalizeUserName(string? userName) => userName?.Trim() ?? string.Empty;
    private static bool IsValidUserName(string? userName) => userName is not null && UserNamePattern.IsMatch(userName.Trim());
}

public sealed record AccountRecord(string UserName, string PasswordHash, string Role, string? AssignedBranchId = null, Guid? StaffId = null);
