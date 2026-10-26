using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ApexPost.Application.Branches;
using ApexPost.Application.Persistence;
using ApexPost.Application.Security;
using ApexPost.Application.Staff;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;
using ApexPost.Web.Controllers;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.Tests;

public sealed class OperationalAuthorizationHttpTests
{
    [Fact]
    public async Task HttpPipelineEnforcesAuthenticationAndApplicationBranchScope()
    {
        var homeBranch = Guid.NewGuid();
        var closedBranch = Guid.NewGuid();
        var missingBranch = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        TestIds.HomeBranch = homeBranch;
        TestIds.StaffId = staffId;
        TestIds.MissingBranch = missingBranch;
        var staff = new StaffMember(EntityId.From(staffId), EmployeeNumber.From("EMP-9100"), "Counter Operator", EntityId.From(homeBranch), [StaffRole.CounterClerk]);
        var branchRepo = new BranchRepository(closedBranch);
        var staffRepo = new StaffRepository(staff);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentActor, HttpCurrentActor>();
        builder.Services.AddSingleton<IBranchRepository>(branchRepo);
        builder.Services.AddSingleton<IStaffRepository>(staffRepo);
        builder.Services.AddSingleton<IAccessAuditWriter, AuditWriter>();
        builder.Services.AddScoped<OperationalAccess>();
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy("CounterOperations", policy => policy.RequireRole("CounterStaff", "BranchManager", "Administrator")));

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapPost("/operations/{branchId:guid}", async (Guid branchId, OperationalAccess access, CancellationToken token) =>
        {
            var result = await access.CheckAsync(branchId, OperationalCapability.Counter, "HttpTest.Write", token);
            if (result.IsSuccess) return Results.Ok();
            var status = result.Errors[0].Code switch
            {
                "actor.required" or "actor.not-found" => StatusCodes.Status401Unauthorized,
                "actor.inactive" or "access.denied" => StatusCodes.Status403Forbidden,
                "branch.not-found" => StatusCodes.Status404NotFound,
                "branch.closed" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };
            return Results.StatusCode(status);
        }).RequireAuthorization("CounterOperations");

        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            var unauthenticated = await client.PostAsync($"/operations/{homeBranch:D}", null);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "signed-test-context");
            var sameBranch = await client.PostAsync($"/operations/{homeBranch:D}", null);
            var crossBranch = await client.PostAsync($"/operations/{Guid.NewGuid():D}", null);
            var missing = await client.PostAsync($"/operations/{missingBranch:D}", null);
            var closed = await client.PostAsync($"/operations/{closedBranch:D}", null);

            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
            Assert.Equal(HttpStatusCode.OK, sameBranch.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, crossBranch.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.Ordinal))
                return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "test.counter"),
                new Claim(ClaimTypes.Name, "test.counter"),
                new Claim(ClaimTypes.Role, "CounterStaff"),
                new Claim(ActorClaimTypes.StaffId, TestIds.StaffId.ToString("D")),
                new Claim(ActorClaimTypes.AssignedBranchId, TestIds.HomeBranch.ToString("D"))
            ], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private static class TestIds
    {
        public static Guid StaffId { get; set; }
        public static Guid HomeBranch { get; set; }
        public static Guid MissingBranch { get; set; }
    }

    private sealed class StaffRepository(StaffMember staff) : IStaffRepository
    {
        public Task<IReadOnlyList<Versioned<StaffMember>>> ListAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<Versioned<StaffMember>>>([]);
        public Task<Versioned<StaffMember>?> GetByIdAsync(EntityId id, CancellationToken token) => Task.FromResult<Versioned<StaffMember>?>(id.Value == staff.Id.Value ? new(staff, 1) : null);
        public Task<bool> TryAddAsync(StaffMember value, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(StaffMember value, long version, CancellationToken token) => Task.FromResult(false);
    }

    private sealed class BranchRepository(Guid closedBranch) : IBranchRepository
    {
        public Task<IReadOnlyList<Versioned<Branch>>> ListAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<Versioned<Branch>>>([]);
        public Task<bool> TryAddAsync(Branch branch, CancellationToken token) => Task.FromResult(false);
        public Task<bool> TryUpdateAsync(Branch branch, long version, CancellationToken token) => Task.FromResult(false);
        public Task<Versioned<Branch>?> GetByIdAsync(EntityId id, CancellationToken token)
        {
            if (id.Value == TestIds.MissingBranch) return Task.FromResult<Versioned<Branch>?>(null);
            var status = id.Value == closedBranch ? BranchStatus.Closed : BranchStatus.Active;
            var branch = Branch.Restore(id, BranchCode.From("TST-910"), "HTTP Test Branch", new BranchAddress("1 Test Road", null, "Test City", "12345", "IN"), 1, status);
            return Task.FromResult<Versioned<Branch>?>(new(branch, 1));
        }
    }

    private sealed class AuditWriter : IAccessAuditWriter
    {
        public Task WriteAdministratorCrossBranchAccessAsync(ActorContext actor, Guid branchId, string operation, CancellationToken token) => Task.CompletedTask;
        public Task WriteOperationAsync(ActorContext? actor, Guid? branchId, string operation, string targetType, string targetReference, string outcome, IReadOnlyDictionary<string, string>? safeMetadata, CancellationToken token) => Task.CompletedTask;
    }
}
