using System.Security.Claims;
using ApexPost.Application.Security;
using ApexPost.Web.Filters;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApexPost.Web.Tests;

public sealed class ActionFilterTests
{
    [Fact]
    public async Task AuditFilterWritesControlledActorAndOutcomeMetadataOnly()
    {
        var writer = new CapturingAuditWriter();
        var http = new DefaultHttpContext();
        http.TraceIdentifier = "corr-filter-test";
        http.Request.Method = "POST";
        http.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ActorClaimTypes.StaffId, "11111111-1111-1111-1111-111111111111"),
            new Claim(ActorClaimTypes.AssignedBranchId, "22222222-2222-2222-2222-222222222222"),
            new Claim(ClaimTypes.Role, "BranchManager")
        ], "test"));
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor(), new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary());
        actionContext.RouteData.Values["controller"] = "Transactions";
        actionContext.RouteData.Values["action"] = "Refund";
        var filters = new List<IFilterMetadata>();
        var executing = new ActionExecutingContext(actionContext, filters, new Dictionary<string, object?>(), new object());
        var filter = new AuditActionFilter(writer);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(new ActionExecutedContext(actionContext, filters, executing.Controller) { Result = new OkResult() }));

        var record = Assert.Single(writer.Records);
        Assert.Equal("11111111-1111-1111-1111-111111111111", record.Actor?.StaffId.ToString("D"));
        Assert.Equal("BranchManager", record.Actor?.Role);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), record.BranchId);
        Assert.Equal("Mvc.ActionCompleted", record.Operation);
        Assert.Equal("Succeeded", record.Outcome);
        Assert.Equal("corr-filter-test", record.Metadata["CorrelationId"]);
        Assert.DoesNotContain("Password", string.Join(',', record.Metadata.Keys));
    }

    [Fact]
    public async Task ValidationFilterReturnsConsistentJsonProblemWithoutInvokingAction()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.Accept = "application/json";
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor(), new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary());
        actionContext.ModelState.AddModelError("input", "invalid");
        var executing = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());
        var called = false;
        var filter = new ValidationActionFilter(NullLogger<ValidationActionFilter>.Instance);

        await filter.OnActionExecutionAsync(executing, () => { called = true; return Task.FromResult(new ActionExecutedContext(actionContext, [], executing.Controller)); });

        Assert.False(called);
        var problem = Assert.IsType<BadRequestObjectResult>(executing.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ValidationProblemDetails>(problem.Value).Status);
    }

    [Fact]
    public async Task AuditFilterDoesNotSwallowAuditPersistenceFailure()
    {
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor(), new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary());
        var filters = new List<IFilterMetadata>();
        var executing = new ActionExecutingContext(actionContext, filters, new Dictionary<string, object?>(), new object());
        var filter = new AuditActionFilter(new CapturingAuditWriter { Failure = new IOException("private storage detail") });

        await Assert.ThrowsAsync<IOException>(() => filter.OnActionExecutionAsync(executing,
            () => Task.FromResult(new ActionExecutedContext(actionContext, filters, executing.Controller) { Result = new OkResult() })));
    }

    [Fact]
    public async Task PerformanceFilterMeasuresSlowActionWithoutRequestData()
    {
        var provider = new CollectingLoggerProvider();
        using var factory = LoggerFactory.Create(logging => logging.AddProvider(provider));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApexPost:Diagnostics:SlowActionThresholdMilliseconds"] = "1"
        }).Build();
        var http = new DefaultHttpContext();
        http.TraceIdentifier = "corr-performance";
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor(), new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary());
        actionContext.RouteData.Values["controller"] = "Shipments";
        actionContext.RouteData.Values["action"] = "Book";
        var filters = new List<IFilterMetadata>();
        var executing = new ActionExecutingContext(actionContext, filters, new Dictionary<string, object?>(), new object());
        var filter = new PerformanceActionFilter(configuration, factory.CreateLogger<PerformanceActionFilter>());
        var called = false;

        await filter.OnActionExecutionAsync(executing, async () =>
        {
            called = true;
            await Task.Delay(10);
            return new ActionExecutedContext(actionContext, filters, executing.Controller);
        });

        Assert.True(called);
        var warning = Assert.Single(provider.Messages);
        Assert.Contains("Slow MVC action", warning, StringComparison.Ordinal);
        Assert.Contains("corr-performance", warning, StringComparison.Ordinal);
        Assert.DoesNotContain("password", warning, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingAuditWriter : IAccessAuditWriter
    {
        public List<AuditCapture> Records { get; } = [];
        public Exception? Failure { get; init; }
        public Task WriteAdministratorCrossBranchAccessAsync(ActorContext actor, Guid branchId, string operation, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task WriteOperationAsync(ActorContext? actor, Guid? branchId, string operation, string targetType, string targetReference, string outcome, IReadOnlyDictionary<string, string>? safeMetadata, CancellationToken cancellationToken)
        {
            if (Failure is not null) return Task.FromException(Failure);
            Records.Add(new(actor, branchId, operation, targetReference, outcome, safeMetadata ?? new Dictionary<string, string>()));
            return Task.CompletedTask;
        }
    }

    private sealed record AuditCapture(ActorContext? Actor, Guid? BranchId, string Operation, string TargetReference, string Outcome, IReadOnlyDictionary<string, string> Metadata);

    private sealed class CollectingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CollectingLogger(Messages);
        public void Dispose() { }
    }

    private sealed class CollectingLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception));
    }
}
