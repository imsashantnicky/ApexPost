using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Web.Controllers;
using ApexPost.Web.Models;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
}
