using ApexPost.Application.Auditing;
using ApexPost.Application.Inventory;
using ApexPost.Application.MoneyOrders;
using ApexPost.Application.Security;
using ApexPost.Application.Shipping;
using ApexPost.Application.Branches;
using ApexPost.Application.Catalogue;
using ApexPost.Application.Reporting;
using ApexPost.Application.Staff;
using ApexPost.Application.Transactions;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Web.Controllers;
using ApexPost.Web.Extensions;
using ApexPost.Web.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ApexPost.Web.Tests;

public sealed class OperationalServiceRegistrationTests : IDisposable
{
    private readonly string dataDirectory = Path.Combine(Path.GetTempPath(), "ApexPostOperationalServices", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ProductionRegistrationsResolveApplicationGraphAndPersistCrossBranchAudit()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Program).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "Testing"
        });
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });
        builder.Services.AddSingleton(new JsonDataOptions { DataDirectory = dataDirectory });
        builder.Services.AddSingleton(new JsonSeedOptions { Enabled = false });
        builder.Services.AddApexPostApplicationServices(builder.Configuration);
        builder.Services.AddScoped<AuditActionFilter>();
        builder.Services.AddScoped<ValidationActionFilter>();
        builder.Services.AddScoped<PerformanceActionFilter>();
        builder.Services.AddControllersWithViews()
            .AddApplicationPart(typeof(Program).Assembly)
            .AddControllersAsServices();

        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();
        var scoped = scope.ServiceProvider;

        Assert.NotNull(scoped.GetRequiredService<OperationalReportService>());
        Assert.NotNull(scoped.GetRequiredService<OperationalAccess>());
        Assert.NotNull(scoped.GetRequiredService<InventoryOperationsUseCase>());
        Assert.NotNull(scoped.GetRequiredService<ShipmentOperationsUseCase>());
        Assert.NotNull(scoped.GetRequiredService<MoneyOrderOperationsUseCase>());
        Assert.IsType<JsonRetailTransactionRepository>(scoped.GetRequiredService<IRetailTransactionRepository>());
        Assert.NotNull(scoped.GetRequiredService<BranchManagementUseCase>());
        Assert.NotNull(scoped.GetRequiredService<StaffManagementUseCase>());
        Assert.NotNull(scoped.GetRequiredService<CatalogueManagementUseCase>());

        foreach (var controller in new Type[]
        {
            typeof(AccountController), typeof(BranchesController), typeof(CatalogueController),
            typeof(DashboardController), typeof(HomeController), typeof(InventoryController),
            typeof(MoneyOrdersController), typeof(ReportsController), typeof(ShipmentsController), typeof(StaffController),
            typeof(TransactionsController), typeof(IntegrationMoneyOrdersController)
        })
        {
            Assert.NotNull(scoped.GetRequiredService(controller));
        }

        Assert.IsType<JsonLinesAuditStore>(scoped.GetRequiredService<IAuditStore>());
        Assert.Same(TimeProvider.System, scoped.GetRequiredService<TimeProvider>());
        await scoped.GetRequiredService<AuditTrail>().RecordCrossBranchAccessAsync(
            new ActorContext(Guid.NewGuid(), "Administrator", Guid.NewGuid(), "registration-test-correlation"),
            Guid.NewGuid(), "Inventory.Read", default);

        var record = Assert.Single(await new JsonLinesAuditStore(dataDirectory).ReadAllAsync(default));
        Assert.Equal("Administrator.CrossBranchAccess", record.Action);
        Assert.Equal("Branch", record.TargetType);
        Assert.Equal("registration-test-correlation", record.CorrelationId);
        Assert.Equal("Inventory.Read", record.Metadata["Operation"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(dataDirectory)) Directory.Delete(dataDirectory, true);
        GC.SuppressFinalize(this);
    }
}
