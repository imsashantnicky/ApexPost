using ApexPost.Application.Persistence;
using ApexPost.Application.Security;
using ApexPost.Application.Staff;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;
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
    private readonly string password = NewPassword();

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
    [InlineData(typeof(BranchesController), "AdministratorOnly")]
    [InlineData(typeof(StaffController), "AdministratorOnly")]
    [InlineData(typeof(CatalogueController), "AdministratorOnly")]
    [InlineData(typeof(InventoryController), "InventoryOperations")]
    [InlineData(typeof(ShipmentsController), "CounterOperations")]
    [InlineData(typeof(MoneyOrdersController), "CounterOperations")]
    public void ControllersRequireNarrowRolePolicies(Type controllerType, string policy)
    {
        var attribute = Assert.Single(controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(policy, attribute.Policy);
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
        var created = await service.CreateAsync("adminuser", password, "Administrator", Guid.NewGuid(), Guid.NewGuid(), default);

        Assert.True(created);
        Assert.Null(await service.ValidateAsync("adminuser", NewPassword(), default));
    }

    [Theory]
    [InlineData(Roles.Administrator)]
    [InlineData(Roles.BranchManager)]
    [InlineData(Roles.CounterStaff)]
    [InlineData(Roles.InventoryStaff)]
    [InlineData(Roles.Auditor)]
    public async Task EachSupportedRoleCanBeCreatedAndValidated(string role)
    {
        var service = new LocalAccountService(new JsonDataOptions { DataDirectory = directory });
        var userName = $"{role.ToLowerInvariant()}user";

        var created = await service.CreateAsync(userName, password, role, Guid.NewGuid(), Guid.NewGuid(), default);

        Assert.True(created);
        var account = await service.ValidateAsync(userName, password, default);
        Assert.NotNull(account);
        Assert.Equal(role, account!.Role);
    }

    [Theory]
    [InlineData("CounterClerk", Roles.CounterStaff)]
    [InlineData("InventoryClerk", Roles.InventoryStaff)]
    [InlineData("OperationsManager", Roles.BranchManager)]
    public void LegacyRoleNamesNormalizeToCurrentSupportedRoles(string legacy, string expected)
    {
        Assert.Equal(expected, StaffRoleMapping.Normalize(legacy));
        Assert.True(StaffRoleMapping.IsSupported(legacy));
    }

    [Fact]
    public async Task InvalidRolesAreRejected()
    {
        var service = new LocalAccountService(new JsonDataOptions { DataDirectory = directory });

        var created = await service.CreateAsync("badroleuser", password, "SuperUser", Guid.NewGuid(), Guid.NewGuid(), default);

        Assert.False(created);
    }

    [Fact]
    public void AuditorRoleIsReadOnlyAndDashboardRoutingMatchesRole()
    {
        Assert.True(StaffRoleMapping.IsReadOnly(Roles.Auditor));
        Assert.Equal("Auditor", LocalAccountService.GetDashboardAction(Roles.Auditor));
        Assert.Equal("Admin", LocalAccountService.GetDashboardAction(Roles.Administrator));
        Assert.Equal("Branch", LocalAccountService.GetDashboardAction(Roles.BranchManager));
        Assert.Equal("Counter", LocalAccountService.GetDashboardAction(Roles.CounterStaff));
        Assert.Equal("Inventory", LocalAccountService.GetDashboardAction(Roles.InventoryStaff));
    }

    [Fact]
    public async Task DevelopmentAccountInitializationCreatesConfiguredRolesOnlyInDevelopment()
    {
        var service = new LocalAccountService(new JsonDataOptions { DataDirectory = directory });
        var staff = new[]
        {
            DevelopmentStaff(1, StaffRole.Administrator), DevelopmentStaff(2, StaffRole.BranchManager),
            DevelopmentStaff(3, StaffRole.CounterClerk), DevelopmentStaff(4, StaffRole.InventoryClerk),
            DevelopmentStaff(5, StaffRole.CounterClerk)
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApexPost:DevelopmentAccounts:Enabled"] = "true",
                ["ApexPost:DevelopmentAccounts:Administrator:UserName"] = "admin.dev",
                ["ApexPost:DevelopmentAccounts:Administrator:Password"] = NewPassword(),
                ["ApexPost:DevelopmentAccounts:Administrator:StaffId"] = staff[0].Id.Value.ToString("D"),
                ["ApexPost:DevelopmentAccounts:BranchManager:UserName"] = "branch.dev",
                ["ApexPost:DevelopmentAccounts:BranchManager:Password"] = NewPassword(),
                ["ApexPost:DevelopmentAccounts:BranchManager:StaffId"] = staff[1].Id.Value.ToString("D"),
                ["ApexPost:DevelopmentAccounts:CounterStaff:UserName"] = "counter.dev",
                ["ApexPost:DevelopmentAccounts:CounterStaff:Password"] = NewPassword(),
                ["ApexPost:DevelopmentAccounts:CounterStaff:StaffId"] = staff[2].Id.Value.ToString("D"),
                ["ApexPost:DevelopmentAccounts:InventoryStaff:UserName"] = "inventory.dev",
                ["ApexPost:DevelopmentAccounts:InventoryStaff:Password"] = NewPassword(),
                ["ApexPost:DevelopmentAccounts:InventoryStaff:StaffId"] = staff[3].Id.Value.ToString("D"),
                ["ApexPost:DevelopmentAccounts:Auditor:UserName"] = "auditor.dev",
                ["ApexPost:DevelopmentAccounts:Auditor:Password"] = NewPassword(),
                ["ApexPost:DevelopmentAccounts:Auditor:StaffId"] = staff[4].Id.Value.ToString("D")
            })
            .Build();

        await service.InitializeDevelopmentAccountsAsync(new TestEnvironment("Development"), config, new DevelopmentStaffRepository(staff), default);

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
                ["ApexPost:DevelopmentAccounts:Administrator:Password"] = NewPassword()
            })
            .Build();

        await service.InitializeDevelopmentAccountsAsync(new TestEnvironment("Production"), config, new DevelopmentStaffRepository([]), default);

        Assert.False(await service.HasAccountAsync("prodadmin.dev", default));
    }

    [Fact]
    public async Task PasswordsAreOnlyStoredAsHashes()
    {
        var service = new LocalAccountService(new JsonDataOptions { DataDirectory = directory });
        await service.CreateAsync("adminuser", password, "Administrator", Guid.NewGuid(), Guid.NewGuid(), default);

        var store = new JsonCollectionStore<AccountRecord>(directory, "accounts.json");
        var accounts = await store.ReadAllAsync(default);
        var record = Assert.Single(accounts.Values);

        Assert.NotEqual(password, record.PasswordHash);
        Assert.DoesNotContain(password, record.PasswordHash, StringComparison.Ordinal);
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

    // Generated per run so no credential-like literal lives in source.
    private static string NewPassword() => $"Test-{Guid.NewGuid():N}!";

    private sealed class TestEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ApexPost.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = environmentName;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static StaffMember DevelopmentStaff(int suffix, StaffRole role) => new(
        EntityId.New(), EmployeeNumber.From($"EMP-{suffix:0000}"), $"Development Staff {suffix}", EntityId.New(), [role]);

    private sealed class DevelopmentStaffRepository(IReadOnlyList<StaffMember> staff) : IStaffRepository
    {
        public Task<IReadOnlyList<Versioned<StaffMember>>> ListAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<Versioned<StaffMember>>>(staff.Select(x => new Versioned<StaffMember>(x, 1)).ToArray());
        public Task<Versioned<StaffMember>?> GetByIdAsync(EntityId id, CancellationToken token) => Task.FromResult(staff.Where(x => x.Id == id).Select(x => (Versioned<StaffMember>?)new(x, 1)).SingleOrDefault());
        public Task<bool> TryAddAsync(StaffMember value, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(StaffMember value, long version, CancellationToken token) => Task.FromResult(false);
    }
}
