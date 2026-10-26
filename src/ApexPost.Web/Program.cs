using ApexPost.Application.Branches;
using ApexPost.Application.Catalogue;
using ApexPost.Application.Inventory;
using ApexPost.Application.MoneyOrders;
using ApexPost.Application.Reporting;
using ApexPost.Application.Shipping;
using ApexPost.Application.Staff;
using ApexPost.Application.Transactions;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Web.Health;
using ApexPost.Web.Filters;
using ApexPost.Web.Middleware;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<AuditActionFilter>();
builder.Services.AddScoped<ValidationActionFilter>();
builder.Services.AddScoped<PerformanceActionFilter>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.AddService<AuditActionFilter>();
    options.Filters.AddService<ValidationActionFilter>();
    options.Filters.AddService<PerformanceActionFilter>();
});
builder.Services.AddHealthChecks()
    .AddCheck<SafeReadinessCheck>("ready")
    .AddCheck<SafeLivenessCheck>("live");
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "ApexPost.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Management", policy => policy.RequireRole(LocalAccountService.AdministratorRole, LocalAccountService.BranchManagerRole, LocalAccountService.CounterStaffRole, LocalAccountService.InventoryStaffRole));
    options.AddPolicy("AdministratorOnly", policy => policy.RequireRole(LocalAccountService.AdministratorRole));
    options.AddPolicy("BranchManagement", policy => policy.RequireRole(LocalAccountService.AdministratorRole, LocalAccountService.BranchManagerRole));
    options.AddPolicy("CounterOperations", policy => policy.RequireRole(LocalAccountService.AdministratorRole, LocalAccountService.BranchManagerRole, LocalAccountService.CounterStaffRole));
    options.AddPolicy("InventoryOperations", policy => policy.RequireRole(LocalAccountService.AdministratorRole, LocalAccountService.BranchManagerRole, LocalAccountService.InventoryStaffRole));
    options.AddPolicy("ReportsAccess", policy => policy.RequireRole(LocalAccountService.AdministratorRole, LocalAccountService.BranchManagerRole, LocalAccountService.AuditorRole));
    options.AddPolicy("AuditorReadOnly", policy => policy.RequireRole(LocalAccountService.AdministratorRole, LocalAccountService.AuditorRole));
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "ApexPost.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("login", limiter => { limiter.PermitLimit = 5; limiter.Window = TimeSpan.FromMinutes(1); limiter.QueueLimit = 0; }));

var dataOptions = new JsonDataOptions();
builder.Configuration.GetSection(JsonDataOptions.SectionName).Bind(dataOptions);
var seedOptions = new JsonSeedOptions();
builder.Configuration.GetSection("ApexPost:JsonSeed").Bind(seedOptions);
builder.Services.AddSingleton(dataOptions);
builder.Services.AddSingleton(seedOptions);
builder.Services.AddSingleton<JsonSeedInitializer>();
builder.Services.AddSingleton<JsonAuditWriter>(serviceProvider => new JsonAuditWriter(serviceProvider.GetRequiredService<JsonDataOptions>().DataDirectory));
builder.Services.AddHostedService<JsonInitializationHostedService>();
builder.Services.AddSingleton<LocalAccountService>();
builder.Services.AddSingleton<IBranchRepository, JsonBranchRepository>();
builder.Services.AddSingleton<IStaffRepository, JsonStaffRepository>();
builder.Services.AddSingleton<IServiceDefinitionRepository, JsonServiceDefinitionRepository>();
builder.Services.AddTransient<BranchManagementUseCase>();
builder.Services.AddTransient<StaffManagementUseCase>();
builder.Services.AddTransient<CatalogueManagementUseCase>();
builder.Services.AddSingleton<IInventoryItemRepository, JsonInventoryItemRepository>();
builder.Services.AddSingleton<IMoneyOrderRepository, JsonMoneyOrderRepository>();
builder.Services.AddSingleton<IRetailTransactionRepository, JsonRetailTransactionRepository>();
builder.Services.AddSingleton<IShipmentRepository, JsonShipmentRepository>();
builder.Services.AddSingleton<IServicePriceLookup, JsonServicePriceLookup>();
builder.Services.AddTransient<InventoryOperationsUseCase>();
builder.Services.AddTransient<ShipmentOperationsUseCase>();
builder.Services.AddTransient<MoneyOrderOperationsUseCase>();
builder.Services.AddTransient<OperationalReportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseHttpsRedirection();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRouting();
app.UseCookiePolicy();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

public partial class Program;
