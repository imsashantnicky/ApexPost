using ApexPost.Application.Auditing;
using ApexPost.Application.Inventory;
using ApexPost.Application.Catalogue;
using ApexPost.Application.Reporting;
using ApexPost.Application.MoneyOrders;
using ApexPost.Application.Security;
using ApexPost.Application.Shipping;
using ApexPost.Application.Staff;
using ApexPost.Application.Branches;
using ApexPost.Application.Transactions;
using ApexPost.Application.IdentityVerification;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Web.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApexPost.Web.Extensions;

public static class OperationalServiceCollectionExtensions
{
    public static IServiceCollection AddApexPostApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentActor, HttpCurrentActor>();
        services.TryAddScoped<OperationalAccess>();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAuditStore>(provider =>
            new JsonLinesAuditStore(provider.GetRequiredService<JsonDataOptions>().DataDirectory));
        services.TryAddSingleton<AuditTrail>();

        services.TryAddSingleton<IBranchRepository, JsonBranchRepository>();
        services.TryAddSingleton<IStaffRepository, JsonStaffRepository>();
        services.AddMemoryCache();
        services.TryAddSingleton<StaffAccessCache>();
        services.TryAddSingleton<IServiceDefinitionRepository, JsonServiceDefinitionRepository>();
        services.TryAddSingleton<IInventoryItemRepository, JsonInventoryItemRepository>();
        services.TryAddSingleton<IMoneyOrderRepository, JsonMoneyOrderRepository>();
        services.TryAddSingleton<IIdentityVerificationRepository, JsonIdentityVerificationRepository>();
        var threshold = configuration.GetValue<decimal?>("ApexPost:MoneyOrders:IdentityVerificationThreshold") ?? 500m;
        services.TryAddSingleton(new MoneyOrderPolicy(threshold));
        var maximumWeight = configuration.GetValue<decimal?>("ApexPost:Shipping:MaximumWeightKilograms") ?? ApexPost.Domain.Shipping.ShipmentPackage.MaximumWeightKilograms;
        services.TryAddSingleton(new ShipmentPolicy(maximumWeight));
        services.TryAddSingleton<IShipmentRepository, JsonShipmentRepository>();
        services.TryAddSingleton<JsonRetailTransactionRepository>();
        services.TryAddSingleton<IRetailTransactionRepository>(provider => provider.GetRequiredService<JsonRetailTransactionRepository>());
        services.TryAddSingleton<ITransactionPaymentCoordinator>(provider => provider.GetRequiredService<JsonRetailTransactionRepository>());
        services.TryAddSingleton<IServicePriceLookup, JsonServicePriceLookup>();
        services.TryAddSingleton<IShippingRateLookup, JsonShippingRateLookup>();

        services.TryAddSingleton<LocalAccountService>();
        services.TryAddScoped<BranchSelectionService>();
        services.TryAddTransient<BranchManagementUseCase>();
        services.TryAddTransient<StaffManagementUseCase>();
        services.TryAddTransient<CatalogueManagementUseCase>();
        services.TryAddTransient<InventoryOperationsUseCase>();
        services.TryAddTransient<ShipmentOperationsUseCase>();
        services.TryAddTransient<MoneyOrderOperationsUseCase>();
        services.TryAddTransient<IdentityVerificationOperationsUseCase>();
        services.TryAddTransient<TransactionOperationsUseCase>();
        services.TryAddTransient<OperationalReportService>();
        return services;
    }
}
