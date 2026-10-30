using System.Net;
using System.Threading.RateLimiting;
using ApexPost.Application.Security;
using ApexPost.Application.Staff;
using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Web.Extensions;
using ApexPost.Web.Filters;
using ApexPost.Web.Health;
using ApexPost.Web.Middleware;
using ApexPost.Web.Security;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
    })
    .AddScheme<AuthenticationSchemeOptions, IntegrationApiKeyAuthenticationHandler>(IntegrationApiKeyDefaults.Scheme, _ => { });
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthorizationHandler, ValidatedStaffHandler>();
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().AddRequirements(new ValidatedStaffRequirement()).Build();
    AddStaffRolePolicy(options, Policies.AdministratorOnly, Roles.Administrator);
    AddStaffRolePolicy(options, Policies.BranchManagement, Roles.Administrator, Roles.BranchManager);
    AddStaffRolePolicy(options, Policies.CounterOperations, Roles.Administrator, Roles.BranchManager, Roles.CounterStaff);
    AddStaffRolePolicy(options, Policies.InventoryOperations, Roles.Administrator, Roles.BranchManager, Roles.InventoryStaff);
    AddStaffRolePolicy(options, Policies.ReportsAccess, Roles.Administrator, Roles.BranchManager, Roles.Auditor);
    AddStaffRolePolicy(options, Policies.AuditorReadOnly, Roles.Administrator, Roles.Auditor);
    AddStaffRolePolicy(options, Policies.IntegrationCounter, Roles.CounterStaff);
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "ApexPost.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

var loginLimit = Math.Clamp(builder.Configuration.GetValue<int?>("ApexPost:Security:LoginPermitLimit") ?? 5, 1, 100);
var partnerLimit = Math.Clamp(builder.Configuration.GetValue<int?>("ApexPost:Security:PartnerPermitLimit") ?? 60, 1, 10_000);
var loginWindow = TimeSpan.FromSeconds(Math.Clamp(builder.Configuration.GetValue<int?>("ApexPost:Security:LoginWindowSeconds") ?? 60, 1, 3600));
var partnerWindow = TimeSpan.FromSeconds(Math.Clamp(builder.Configuration.GetValue<int?>("ApexPost:Security:PartnerWindowSeconds") ?? 60, 1, 3600));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        $"login:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = loginLimit, Window = loginWindow, QueueLimit = 0, AutoReplenishment = true }));
    options.AddPolicy("partner", context => RateLimitPartition.GetFixedWindowLimiter(
        $"partner:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = partnerLimit, Window = partnerWindow, QueueLimit = 0, AutoReplenishment = true }));
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Too many requests.")
            .ExecuteAsync(context.HttpContext);
    };
});
builder.Services.AddProblemDetails();
builder.Services.AddHsts(options =>
{
    var maxAgeDays = Math.Clamp(builder.Configuration.GetValue<int?>("ApexPost:Security:HstsMaxAgeDays") ?? 180, 1, 730);
    options.MaxAge = TimeSpan.FromDays(maxAgeDays);
    options.IncludeSubDomains = true;
});
var trustedProxyAddresses = (builder.Configuration.GetSection("ApexPost:Security:TrustedProxies").Get<string[]>() ?? [])
    .Select(proxy => IPAddress.TryParse(proxy, out var address)
        ? address
        : throw new InvalidOperationException("Trusted proxy configuration contains an invalid address."))
    .ToArray();
builder.Services.PostConfigure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = trustedProxyAddresses.Length == 0
        ? ForwardedHeaders.None
        : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
    foreach (var address in trustedProxyAddresses) options.KnownProxies.Add(address);
});
var approvedOrigins = builder.Configuration.GetSection("ApexPost:Security:ApprovedCorsOrigins").Get<string[]>() ?? [];
foreach (var origin in approvedOrigins)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var parsed) || parsed is null || parsed.AbsolutePath != "/" || !string.IsNullOrEmpty(parsed.Query) || !string.IsNullOrEmpty(parsed.Fragment) || parsed.Scheme is not ("http" or "https"))
        throw new InvalidOperationException("CORS origin configuration contains an invalid origin.");
}
builder.Services.AddCors(options => options.AddPolicy("ApprovedOrigins", policy =>
{
    if (approvedOrigins.Length > 0) policy.WithOrigins(approvedOrigins);
    else policy.SetIsOriginAllowed(static _ => false);
    policy.AllowAnyHeader().AllowAnyMethod();
}));

var dataOptions = new JsonDataOptions();
builder.Configuration.GetSection(JsonDataOptions.SectionName).Bind(dataOptions);
var seedOptions = new JsonSeedOptions();
builder.Configuration.GetSection("ApexPost:JsonSeed").Bind(seedOptions);
builder.Services.AddSingleton(dataOptions);
builder.Services.AddSingleton(seedOptions);
builder.Services.AddSingleton<JsonSeedInitializer>();
builder.Services.AddHostedService<JsonInitializationHostedService>();
builder.Services.AddApexPostApplicationServices(builder.Configuration);

var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseMiddleware<MaintenanceModeMiddleware>();
app.UseHttpsRedirection();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRouting();
app.UseCors("ApprovedOrigins");
app.UseCookiePolicy();
app.UseRateLimiter();
app.UseMiddleware<IntegrationIpAllowlistMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live");
app.MapControllers();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
app.Run();

static void AddStaffRolePolicy(AuthorizationOptions options, string name, params string[] roles) =>
    options.AddPolicy(name, policy => policy.RequireAuthenticatedUser().RequireRole(roles).AddRequirements(new ValidatedStaffRequirement()));

public partial class Program;
