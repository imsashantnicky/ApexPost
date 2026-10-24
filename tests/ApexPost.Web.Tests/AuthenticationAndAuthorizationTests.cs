using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Web.Controllers;
using ApexPost.Web.Models;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace ApexPost.Web.Tests;

public class AuthenticationAndAuthorizationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ApexPostWebTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void DashboardRequiresAuthentication()
    {
        var attributes = typeof(DashboardController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .ToArray();

        Assert.Single(attributes);
    }

    [Theory]
    [InlineData(typeof(BranchesController))]
    [InlineData(typeof(StaffController))]
    [InlineData(typeof(CatalogueController))]
    [InlineData(typeof(InventoryController))]
    [InlineData(typeof(ShipmentsController))]
    [InlineData(typeof(MoneyOrdersController))]
    public void ManagementControllersRequireManagementPolicy(Type controllerType)
    {
        var attribute = Assert.Single(controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("Management", attribute.Policy);
    }

    [Fact]
    public void ProvisionRequiresAdministratorOnlyPolicy()
    {
        var method = typeof(AccountController).GetMethod(nameof(AccountController.Provision), [typeof(ProvisionAccountViewModel), typeof(CancellationToken)]);
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("AdministratorOnly", attribute.Policy);
    }

    [Fact]
    public async Task InvalidCredentialsAreRejected()
    {
        var service = new LocalAccountService(new JsonDataOptions { DataDirectory = directory });
        var created = await service.CreateAsync("adminuser", "StrongPass123!", "Administrator", default);

        Assert.True(created);
        Assert.Null(await service.ValidateAsync("adminuser", "WrongPass123!", default));
    }

    [Theory]
    [InlineData(LocalAccountService.AdministratorRole)]
    [InlineData(LocalAccountService.BranchManagerRole)]
    [InlineData(LocalAccountService.CounterStaffRole)]
    [InlineData(LocalAccountService.InventoryStaffRole)]
    [InlineData(LocalAccountService.AuditorRole)]
    public async Task EachSupportedRoleCanBeCreatedAndValidated(string role)
    {
        var service = new LocalAccountService(new JsonDataOptions { DataDirectory = directory });
        var userName = $"{role.ToLowerInvariant()}user";

        var created = await service.CreateAsync(userName, "StrongPass123!", role, default);

        Assert.True(created);
        var account = await service.ValidateAsync(userName, "StrongPass123!", default);
        Assert.NotNull(account);
        Assert.Equal(role, account!.Role);
    }

    [Theory]
    [InlineData("CounterClerk", LocalAccountService.CounterStaffRole)]
    [InlineData("InventoryClerk", LocalAccountService.InventoryStaffRole)]
    [InlineData("OperationsManager", LocalAccountService.BranchManagerRole)]
    public void LegacyRoleNamesNormalizeToCurrentSupportedRoles(string legacy, string expected)
    {
        Assert.Equal(expected, LocalAccountService.NormalizeRole(legacy));
        Assert.True(LocalAccountService.IsSupportedRole(legacy));
    }

    [Fact]
    public async Task InvalidRolesAreRejected()
    {
        var service = new LocalAccountService(new JsonDataOptions { DataDirectory = directory });

        var created = await service.CreateAsync("badroleuser", "StrongPass123!", "SuperUser", default);

        Assert.False(created);
    }

    [Fact]
    public void BranchAccessIsRestrictedToAssignedBranchForNonAdministrators()
    {
        Assert.True(LocalAccountService.CanAccessBranch(LocalAccountService.AdministratorRole, "BR-001", "BR-999"));
        Assert.True(LocalAccountService.CanAccessBranch(LocalAccountService.AdministratorRole, null, "BR-999"));
        Assert.False(LocalAccountService.CanAccessBranch(LocalAccountService.CounterStaffRole, "BR-001", "BR-002"));
        Assert.True(LocalAccountService.CanAccessBranch(LocalAccountService.CounterStaffRole, "BR-001", "BR-001"));
        Assert.True(LocalAccountService.CanAccessBranch(LocalAccountService.AuditorRole, "BR-001", "BR-999"));
    }

    [Fact]
    public void AuditorRoleIsReadOnlyAndDashboardRoutingMatchesRole()
    {
        Assert.True(LocalAccountService.IsReadOnlyRole(LocalAccountService.AuditorRole));
        Assert.Equal("Auditor", LocalAccountService.GetDashboardAction(LocalAccountService.AuditorRole));
        Assert.Equal("Admin", LocalAccountService.GetDashboardAction(LocalAccountService.AdministratorRole));
        Assert.Equal("Branch", LocalAccountService.GetDashboardAction(LocalAccountService.BranchManagerRole));
        Assert.Equal("Counter", LocalAccountService.GetDashboardAction(LocalAccountService.CounterStaffRole));
        Assert.Equal("Inventory", LocalAccountService.GetDashboardAction(LocalAccountService.InventoryStaffRole));
    }

    [Fact]
    public async Task DevelopmentAccountInitializationCreatesConfiguredRolesOnlyInDevelopment()
    {
        var service = new LocalAccountService(new JsonDataOptions { DataDirectory = directory });
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApexPost:DevelopmentAccounts:Enabled"] = "true",
                ["ApexPost:DevelopmentAccounts:Administrator:UserName"] = "admin.dev",
                ["ApexPost:DevelopmentAccounts:Administrator:Password"] = "ChangeThis-StrongDevPass-123!",
                ["ApexPost:DevelopmentAccounts:BranchManager:UserName"] = "branch.dev",
                ["ApexPost:DevelopmentAccounts:BranchManager:Password"] = "ChangeThis-StrongDevPass-456!",
                ["ApexPost:DevelopmentAccounts:CounterStaff:UserName"] = "counter.dev",
                ["ApexPost:DevelopmentAccounts:CounterStaff:Password"] = "ChangeThis-StrongDevPass-789!",
                ["ApexPost:DevelopmentAccounts:InventoryStaff:UserName"] = "inventory.dev",
                ["ApexPost:DevelopmentAccounts:InventoryStaff:Password"] = "ChangeThis-StrongDevPass-101!",
                ["ApexPost:DevelopmentAccounts:Auditor:UserName"] = "auditor.dev",
                ["ApexPost:DevelopmentAccounts:Auditor:Password"] = "ChangeThis-StrongDevPass-202!"
            })
            .Build();

        await service.InitializeDevelopmentAccountsAsync(new TestEnvironment("Development"), config, default);

        Assert.True(await service.HasAccountAsync("admin.dev", default));
        Assert.True(await service.HasAccountAsync("branch.dev", default));
        Assert.True(await service.HasAccountAsync("counter.dev", default));
        Assert.True(await service.HasAccountAsync("inventory.dev", default));
        Assert.True(await service.HasAccountAsync("auditor.dev", default));
    }

    [Fact]
    public async Task ProductionEnvironmentSkipsDevelopmentAccountInitialization()
    {
        var service = new LocalAccountService(new JsonDataOptions { DataDirectory = directory });
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApexPost:DevelopmentAccounts:Enabled"] = "true",
                ["ApexPost:DevelopmentAccounts:Administrator:UserName"] = "prodadmin.dev",
                ["ApexPost:DevelopmentAccounts:Administrator:Password"] = "ChangeThis-StrongDevPass-123!"
            })
            .Build();

        await service.InitializeDevelopmentAccountsAsync(new TestEnvironment("Production"), config, default);

        Assert.False(await service.HasAccountAsync("prodadmin.dev", default));
    }

    [Fact]
    public async Task PasswordsAreOnlyStoredAsHashes()
    {
        var service = new LocalAccountService(new JsonDataOptions { DataDirectory = directory });
        await service.CreateAsync("adminuser", "StrongPass123!", "Administrator", default);

        var store = new JsonCollectionStore<AccountRecord>(directory, "accounts.json");
        var accounts = await store.ReadAllAsync(default);
        var record = Assert.Single(accounts.Values);

        Assert.NotEqual("StrongPass123!", record.PasswordHash);
        Assert.DoesNotContain("StrongPass123!", record.PasswordHash, StringComparison.Ordinal);
        Assert.NotEmpty(record.PasswordHash);
    }

    [Fact]
    public void LoginFormIsProtectedByAntiforgery()
    {
        var method = typeof(AccountController).GetMethod(nameof(AccountController.Login), [typeof(LoginViewModel), typeof(CancellationToken)]);
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).Cast<ValidateAntiForgeryTokenAttribute>());
        Assert.NotNull(attribute);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }

        GC.SuppressFinalize(this);
    }

    private sealed class TestEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ApexPost.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = environmentName;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
