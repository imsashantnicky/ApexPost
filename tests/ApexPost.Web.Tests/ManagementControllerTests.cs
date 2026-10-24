using ApexPost.Application.Branches;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Web.Controllers;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Tests;

public class ManagementControllerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ApexPostWebTests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(typeof(BranchesController))]
    [InlineData(typeof(StaffController))]
    [InlineData(typeof(CatalogueController))]
    [InlineData(typeof(InventoryController))]
    [InlineData(typeof(ShipmentsController))]
    public void ManagementControllersRequireManagementPolicy(Type controllerType)
    {
        var attribute = Assert.Single(controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("Management", attribute.Policy);
    }

    [Fact]
    public async Task BranchCreateWithInvalidSubmissionReturnsViewAndValidationErrors()
    {
        var repository = new JsonBranchRepository(new JsonDataOptions { DataDirectory = directory });
        var controller = new BranchesController(new BranchManagementUseCase(repository));
        controller.ModelState.AddModelError("Name", "Required");

        var result = await controller.Create(new BranchForm(), default);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task BranchCreatePersistsAndRedirectsOnValidSubmission()
    {
        var repository = new JsonBranchRepository(new JsonDataOptions { DataDirectory = directory });
        var controller = new BranchesController(new BranchManagementUseCase(repository));
        var model = new BranchForm { Code = "BLR-009", Name = "Test Branch", AddressLine1 = "1 Test Road", City = "Bengaluru", PostalCode = "560001", CountryCode = "IN", CounterCount = 1 };

        var result = await controller.Create(model, default);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Single(await repository.ListAsync(default));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }
}
