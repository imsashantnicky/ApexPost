using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ApexPost.Application.Persistence;
using ApexPost.Application.Security;
using ApexPost.Application.Staff;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;
using ApexPost.Web.Middleware;
using ApexPost.Web.Security;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using System.Threading.RateLimiting;

namespace ApexPost.Web.Tests;

public sealed class SecurityHardeningHttpTests
{
    private const string Key = "phase-three-test-key-do-not-log";

    [Fact]
    public async Task IntegrationEndpointRequiresApiKeyAndUsesStaffRecordForClaims()
    {
        await using var host = await StartHostAsync();
        var missing = await host.Client.GetAsync("/api/integrations/money-orders");
        host.Client.DefaultRequestHeaders.Add(IntegrationApiKeyAuthenticationHandler.HeaderName, "not-the-key");
        var invalid = await host.Client.GetAsync("/api/integrations/money-orders");
        host.Client.DefaultRequestHeaders.Remove(IntegrationApiKeyAuthenticationHandler.HeaderName);
        host.Client.DefaultRequestHeaders.Add(IntegrationApiKeyAuthenticationHandler.HeaderName, Key);
        var valid = await host.Client.GetAsync("/api/integrations/money-orders");
        var validBody = await valid.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        Assert.Contains(host.StaffId.ToString("D"), validBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Key, validBody, StringComparison.Ordinal);
        Assert.Equal("nosniff", valid.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task MissingApiKeyConfigurationFailsClosedWithUnauthorized()
    {
        await using var host = await StartHostAsync(includeKey: false);
        var response = await host.Client.GetAsync("/api/integrations/money-orders");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ApiKeyDoesNotAuthenticateMissingOrInactiveStaff()
    {
        await using var missingStaffHost = await StartHostAsync(staffExists: false);
        missingStaffHost.Client.DefaultRequestHeaders.Add(IntegrationApiKeyAuthenticationHandler.HeaderName, Key);
        var missingStaff = await missingStaffHost.Client.GetAsync("/api/integrations/money-orders");

        await using var inactiveStaffHost = await StartHostAsync(staffStatus: EmploymentStatus.Suspended);
        inactiveStaffHost.Client.DefaultRequestHeaders.Add(IntegrationApiKeyAuthenticationHandler.HeaderName, Key);
        var inactiveStaff = await inactiveStaffHost.Client.GetAsync("/api/integrations/money-orders");

        Assert.Equal(HttpStatusCode.Unauthorized, missingStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, inactiveStaff.StatusCode);
    }

    [Fact]
    public async Task UntrustedForwardedIpCannotBypassIntegrationAllowlist()
    {
        await using var host = await StartHostAsync(allowedIp: "192.0.2.77");
        host.Client.DefaultRequestHeaders.Add(IntegrationApiKeyAuthenticationHandler.HeaderName, Key);
        host.Client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.0.2.77");
        var blocked = await host.Client.GetAsync("/api/integrations/money-orders");
        var health = await host.Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task EmptyIntegrationIpAllowlistFailsClosed()
    {
        await using var host = await StartHostAsync(allowedIp: null);
        host.Client.DefaultRequestHeaders.Add(IntegrationApiKeyAuthenticationHandler.HeaderName, Key);
        var response = await host.Client.GetAsync("/api/integrations/money-orders");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ForwardedClientIpIsAcceptedOnlyFromConfiguredProxy()
    {
        await using var host = await StartHostAsync(allowedIp: "192.0.2.77", trustLoopbackProxy: true);
        host.Client.DefaultRequestHeaders.Add(IntegrationApiKeyAuthenticationHandler.HeaderName, Key);
        host.Client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.0.2.77");
        var response = await host.Client.GetAsync("/api/integrations/money-orders");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MaintenanceReturns503RetryAfterAndLeavesHealthAvailable()
    {
        await using var host = await StartHostAsync(maintenance: true);
        var unavailable = await host.Client.GetAsync("/api/integrations/money-orders");
        var health = await host.Client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal("45", unavailable.Headers.GetValues("Retry-After").Single());
        Assert.Equal("application/problem+json", unavailable.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Fact]
    public async Task PartnerRateLimitAndCorsOriginPolicyAreApplied()
    {
        await using var host = await StartHostAsync(partnerPermitLimit: 1);
        host.Client.DefaultRequestHeaders.Add(IntegrationApiKeyAuthenticationHandler.HeaderName, Key);
        var first = await host.Client.GetAsync("/api/integrations/money-orders");
        var limited = await host.Client.GetAsync("/api/integrations/money-orders");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);

        using var options = new HttpRequestMessage(HttpMethod.Options, "/api/integrations/money-orders");
        options.Headers.Add("Origin", "https://trusted.example");
        options.Headers.Add("Access-Control-Request-Method", "GET");
        var preflight = await host.Client.SendAsync(options);
        Assert.Equal("https://trusted.example", preflight.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task LoginRateLimitIsAppliedPerRemoteAddress()
    {
        await using var host = await StartHostAsync(loginPermitLimit: 2);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync("/login-limit-test", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsync("/login-limit-test", null)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await host.Client.PostAsync("/login-limit-test", null)).StatusCode);
    }

    [Fact]
    public async Task CounterRoleCannotUseAdministratorOnlyEndpoint()
    {
        await using var host = await StartHostAsync();
        host.Client.DefaultRequestHeaders.Add(IntegrationApiKeyAuthenticationHandler.HeaderName, Key);
        var response = await host.Client.GetAsync("/admin-only-test");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuditorCanReadReportsButCannotUseWritePolicy()
    {
        await using var host = await StartHostAsync();
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("TestRole", "Auditor");
        var report = await host.Client.GetAsync("/report-read-test");
        var write = await host.Client.PostAsync("/counter-write-test", null);
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    private static async Task<TestHost> StartHostAsync(
        bool includeKey = true,
        string? allowedIp = "127.0.0.1",
        bool maintenance = false,
        int partnerPermitLimit = 10,
        int loginPermitLimit = 5,
        bool trustLoopbackProxy = false,
        bool staffExists = true,
        EmploymentStatus staffStatus = EmploymentStatus.Active)
    {
        var staffId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var auditorStaffId = Guid.NewGuid();
        var staff = new StaffMember(EntityId.From(staffId), EmployeeNumber.From("EMP-9901"), "Integration operator", EntityId.From(branchId), [StaffRole.CounterClerk]);
        var auditorStaff = new StaffMember(EntityId.From(auditorStaffId), EmployeeNumber.From("EMP-9902"), "Auditor", EntityId.From(branchId), [StaffRole.CounterClerk]);
        if (staffStatus == EmploymentStatus.Suspended) staff.Suspend();
        else if (staffStatus == EmploymentStatus.Terminated) staff.Terminate();
        var settings = new Dictionary<string, string?>
        {
            ["ApexPost:Integration:StaffId"] = staffId.ToString("D"),
            ["ApexPost:TestAuditor:StaffId"] = auditorStaffId.ToString("D"),
            ["ApexPost:TestAuditor:BranchId"] = branchId.ToString("D"),
            ["ApexPost:Integration:ApiKey"] = includeKey ? Key : null,
            ["ApexPost:Security:TrustedProxies:0"] = trustLoopbackProxy ? "127.0.0.1" : null,
            ["ApexPost:Maintenance:Enabled"] = maintenance.ToString(),
            ["ApexPost:Maintenance:RetryAfterSeconds"] = "45",
            ["ApexPost:Security:ApprovedCorsOrigins:0"] = "https://trusted.example",
            ["ApexPost:Security:PartnerPermitLimit"] = partnerPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ApexPost:Security:LoginPermitLimit"] = loginPermitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        if (allowedIp is not null) settings["ApexPost:Security:IntegrationAllowedIPs:0"] = allowedIp;
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IStaffRepository>(new StaffRepositoryDouble(staffExists ? [staff, auditorStaff] : [auditorStaff]));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IAuthorizationHandler, ValidatedStaffHandler>();
        builder.Services.AddAuthentication("Cookie")
            .AddScheme<AuthenticationSchemeOptions, IntegrationApiKeyAuthenticationHandler>(IntegrationApiKeyDefaults.Scheme, _ => { })
            .AddScheme<AuthenticationSchemeOptions, TestRoleAuthenticationHandler>("TestRole", _ => { });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("IntegrationCounter", policy => policy.AddAuthenticationSchemes(IntegrationApiKeyDefaults.Scheme)
                .RequireRole("CounterStaff").AddRequirements(new ValidatedStaffRequirement()));
            options.AddPolicy("AdminOnly", policy => policy.AddAuthenticationSchemes(IntegrationApiKeyDefaults.Scheme)
                .RequireRole("Administrator").AddRequirements(new ValidatedStaffRequirement()));
            options.AddPolicy("ReportsAccess", policy => policy.AddAuthenticationSchemes("TestRole")
                .RequireRole("Administrator", "BranchManager", "Auditor").AddRequirements(new ValidatedStaffRequirement()));
            options.AddPolicy("CounterOperations", policy => policy.AddAuthenticationSchemes("TestRole")
                .RequireRole("Administrator", "BranchManager", "CounterStaff").AddRequirements(new ValidatedStaffRequirement()));
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("partner", context => RateLimitPartition.GetFixedWindowLimiter(
                $"partner:{context.Connection.RemoteIpAddress}", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = partnerPermitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                $"login:{context.Connection.RemoteIpAddress}", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = loginPermitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
            options.OnRejected = async (context, token) =>
            {
                await Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Too many requests.")
                    .ExecuteAsync(context.HttpContext);
            };
        });
        builder.Services.PostConfigure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = trustLoopbackProxy
                ? ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
                : ForwardedHeaders.None;
            options.ForwardLimit = 1;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            if (trustLoopbackProxy) options.KnownProxies.Add(IPAddress.Loopback);
        });
        builder.Services.AddCors(options => options.AddPolicy("ApprovedOrigins", policy => policy
            .WithOrigins("https://trusted.example").AllowAnyHeader().AllowAnyMethod()));

        var app = builder.Build();
        app.UseForwardedHeaders();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<MaintenanceModeMiddleware>();
        app.UseRouting();
        app.UseCors("ApprovedOrigins");
        app.UseRateLimiter();
        app.UseMiddleware<IntegrationIpAllowlistMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/api/integrations/money-orders", (ClaimsPrincipal user, HttpContext context) => Results.Ok(new { staffId = user.FindFirstValue(ActorClaimTypes.StaffId), remoteIp = context.Connection.RemoteIpAddress?.ToString() }))
            .WithMetadata(new IntegrationIpRestrictedAttribute())
            .RequireAuthorization("IntegrationCounter")
            .RequireRateLimiting("partner");
        app.MapGet("/admin-only-test", () => Results.Ok()).RequireAuthorization("AdminOnly");
        app.MapGet("/report-read-test", () => Results.Ok()).RequireAuthorization("ReportsAccess");
        app.MapPost("/counter-write-test", () => Results.Ok()).RequireAuthorization("CounterOperations");
        app.MapPost("/login-limit-test", () => Results.Ok()).RequireRateLimiting("login");
        app.MapGet("/health", () => Results.Ok("healthy"));
        app.MapGet("/health/ready", () => Results.Ok("ready"));
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        return new TestHost(app, new HttpClient { BaseAddress = new Uri(address) }, staffId);
    }

    private sealed class TestHost(WebApplication app, HttpClient client, Guid staffId) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public Guid StaffId { get; } = staffId;
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private sealed class StaffRepositoryDouble(IReadOnlyList<StaffMember> staff) : IStaffRepository
    {
        public Task<IReadOnlyList<Versioned<StaffMember>>> ListAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<Versioned<StaffMember>>>(staff.Select(x => new Versioned<StaffMember>(x, 1)).ToArray());
        public Task<Versioned<StaffMember>?> GetByIdAsync(EntityId id, CancellationToken token) => Task.FromResult(staff.Where(x => id == x.Id).Select(x => (Versioned<StaffMember>?)new(x, 1)).SingleOrDefault());
        public Task<bool> TryAddAsync(StaffMember value, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(StaffMember value, long version, CancellationToken token) => Task.FromResult(false);
    }

    private sealed class TestRoleAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers.Authorization.ToString().Split(' ', 2).LastOrDefault();
            if (role != "Auditor") return Task.FromResult(AuthenticateResult.NoResult());
            var staffId = configuration["ApexPost:TestAuditor:StaffId"];
            var branchId = configuration["ApexPost:TestAuditor:BranchId"];
            if (!Guid.TryParse(staffId, out var parsedStaffId) || !Guid.TryParse(branchId, out var parsedBranchId))
                return Task.FromResult(AuthenticateResult.Fail("Invalid test identity."));
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, parsedStaffId.ToString("D")),
                new Claim(ClaimTypes.Name, "test auditor"),
                new Claim(ClaimTypes.Role, "Auditor"),
                new Claim(ActorClaimTypes.StaffId, parsedStaffId.ToString("D")),
                new Claim(ActorClaimTypes.AssignedBranchId, parsedBranchId.ToString("D"))
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
