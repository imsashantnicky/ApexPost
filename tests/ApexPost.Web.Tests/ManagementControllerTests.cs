using ApexPost.Application.Branches;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Application.Security;
using ApexPost.Web.Controllers;
using ApexPost.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Http;

namespace ApexPost.Web.Tests;

public class ManagementControllerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ApexPostWebTests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(typeof(BranchesController), "AdministratorOnly")]
    [InlineData(typeof(StaffController), "AdministratorOnly")]
    [InlineData(typeof(CatalogueController), "AdministratorOnly")]
    [InlineData(typeof(InventoryController), "InventoryOperations")]
    [InlineData(typeof(ShipmentsController), "CounterOperations")]
    [InlineData(typeof(TransactionsController), "CounterOperations")]
    [InlineData(typeof(MoneyOrdersController), "CounterOperations")]
    [InlineData(typeof(ReportsController), "ReportsAccess")]
    public void ControllersRequireTheNarrowestMatchingPolicy(Type controllerType, string policy)
    {
        var attribute = Assert.Single(controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(policy, attribute.Policy);
    }

    [Theory]
    [InlineData(typeof(BranchesController), "Create")]
    [InlineData(typeof(StaffController), "Create")]
    [InlineData(typeof(CatalogueController), "Create")]
    [InlineData(typeof(InventoryController), "Receive")]
    [InlineData(typeof(InventoryController), "Issue")]
    [InlineData(typeof(TransactionsController), "Create")]
    [InlineData(typeof(TransactionsController), "AddLine")]
    [InlineData(typeof(TransactionsController), "StartPayment")]
    [InlineData(typeof(TransactionsController), "Void")]
    [InlineData(typeof(TransactionsController), "Refund")]
    [InlineData(typeof(ShipmentsController), "Book")]
    [InlineData(typeof(MoneyOrdersController), "Issue")]
    [InlineData(typeof(MoneyOrdersController), "StartVerification")]
    [InlineData(typeof(MoneyOrdersController), "CompleteVerification")]
    public void StateChangingMvcActionsRequireAntiforgery(Type controllerType, string actionName)
    {
        var action = Assert.Single(controllerType.GetMethods(), method => method.Name == actionName && method.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0);
        Assert.Contains(action.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true), _ => true);
    }

    [Fact]
    public async Task BranchCreateWithInvalidSubmissionReturnsViewAndValidationErrors()
    {
        var repository = new JsonBranchRepository(new JsonDataOptions { DataDirectory = directory });
        var controller = new BranchesController(NewBranchUseCase(repository));
        controller.ModelState.AddModelError("Name", "Required");

        var result = await controller.Create(new BranchForm(), default);

        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task BranchCreatePersistsAndRedirectsOnValidSubmission()
    {
        var repository = new JsonBranchRepository(new JsonDataOptions { DataDirectory = directory });
        var controller = new BranchesController(NewBranchUseCase(repository));
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider());
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

    private BranchManagementUseCase NewBranchUseCase(IBranchRepository repository) => new(repository, new TestActor(), new ApexPost.Application.Auditing.AuditTrail(new JsonLinesAuditStore(directory), TimeProvider.System));
    private sealed class TestActor : ICurrentActor { public ActorContext? Actor => null; }
    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
