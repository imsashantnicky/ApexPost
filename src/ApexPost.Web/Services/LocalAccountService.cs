using ApexPost.Infrastructure.Persistence.Json;
using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;

namespace ApexPost.Web.Services;

public sealed class LocalAccountService
{
    private static readonly SemaphoreSlim BootstrapLock = new(1, 1);
    private static readonly Regex UserNamePattern = new("^[A-Za-z0-9._-]{3,80}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.Ordinal)
    {
        "Administrator", "BranchManager", "CounterClerk"
    };

    private readonly JsonCollectionStore<AccountRecord> store;
    private readonly PasswordHasher<AccountRecord> hasher = new();

    public LocalAccountService(JsonDataOptions options) => store = new JsonCollectionStore<AccountRecord>(options.DataDirectory, "accounts.json");
    public async Task<bool> HasAccountsAsync(CancellationToken token) => (await store.ReadAllAsync(token)).Count > 0;
    public async Task<bool> BootstrapAsync(string userName, string password, CancellationToken token)
    {
        await BootstrapLock.WaitAsync(token);
        try
        {
            if (!IsValidUserName(userName) || password is null || password.Length < 12 || await HasAccountsAsync(token)) return false;
            return await CreateAsync(userName, password, "Administrator", token);
        }
        finally
        {
            BootstrapLock.Release();
        }
    }

    public async Task<bool> CreateAsync(string userName, string password, string role, CancellationToken token)
    {
        var normalized = NormalizeUserName(userName);
        if (!IsValidUserName(normalized) || password is null || password.Length < 12 || !AllowedRoles.Contains(role)) return false;

        var record = new AccountRecord(normalized, string.Empty, role);
        record = record with { PasswordHash = hasher.HashPassword(record, password) };
        return await store.TryAddAsync(normalized.ToLowerInvariant(), record, token);
    }
    public async Task<AccountRecord?> ValidateAsync(string userName, string password, CancellationToken token)
    {
        var normalized = NormalizeUserName(userName);
        var accounts = await store.ReadAllAsync(token);
        if (!accounts.TryGetValue(normalized.ToLowerInvariant(), out var account)) return null;
        if (password is null) return null;
        return hasher.VerifyHashedPassword(account, account.PasswordHash, password) is not PasswordVerificationResult.Failed ? account : null;
    }

    private static string NormalizeUserName(string? userName) => userName?.Trim() ?? string.Empty;
    private static bool IsValidUserName(string? userName) => userName is not null && UserNamePattern.IsMatch(userName.Trim());
}

public sealed record AccountRecord(string UserName, string PasswordHash, string Role);
