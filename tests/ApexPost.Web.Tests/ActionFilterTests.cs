using System.Security.Claims;
using ApexPost.Application.Auditing;
using ApexPost.Application.Security;
using ApexPost.Infrastructure.Persistence.Json;
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
using Microsoft.Extensions.Time.Testing;

namespace ApexPost.Web.Tests;

public sealed class ActionFilterTests
{
    [Fact]
    public async Task AuditFilterWritesControlledActorAndOutcomeMetadataOnly()
    {
        var store = new InMemoryAuditStore();
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
        var filter = new AuditActionFilter(store.Trail(), TimeProvider.System);

        await filter.OnActionExecutionAsync(executing, () => Task.FromResult(new ActionExecutedContext(actionContext, filters, executing.Controller) { Result = new OkResult() }));

        var record = Assert.Single(store.Entries);
        Assert.Equal("11111111-1111-1111-1111-111111111111", record.StaffId?.ToString("D"));
        Assert.Equal("BranchManager", record.Role);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), record.BranchId);
        Assert.Equal("Mvc.ActionCompleted", record.Action);
        Assert.Equal("Transactions.Refund", record.TargetReference);
        Assert.Equal("Transactions", record.Metadata["Controller"]);
        Assert.Equal("Refund", record.Metadata["Action"]);
        Assert.Equal("Succeeded", record.Outcome);
        Assert.Equal("corr-filter-test", record.CorrelationId);
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
        var (executing, actionContext) = Context("POST", "Transactions", "Create");
        var filter = new AuditActionFilter(new InMemoryAuditStore { Failure = new IOException("private storage detail") }.Trail(), TimeProvider.System);

        await Assert.ThrowsAsync<IOException>(() => filter.OnActionExecutionAsync(executing,
            () => Task.FromResult(new ActionExecutedContext(actionContext, [], executing.Controller) { Result = new OkResult() })));
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
        var time = new FakeTimeProvider();
        var filter = new PerformanceActionFilter(configuration, factory.CreateLogger<PerformanceActionFilter>(), time);
        var called = false;

        await filter.OnActionExecutionAsync(executing, () =>
        {
            called = true;
            time.Advance(TimeSpan.FromMilliseconds(10));
            return Task.FromResult(new ActionExecutedContext(actionContext, filters, executing.Controller));
        });

        Assert.True(called);
        var warning = Assert.Single(provider.Messages);
        Assert.Contains("Slow MVC action", warning, StringComparison.Ordinal);
        Assert.Contains("corr-performance", warning, StringComparison.Ordinal);
        Assert.DoesNotContain("password", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditFilter_CorrelationIdContainingKey_DoesNotFailTheAction()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ApexPostFilterTests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonLinesAuditStore(directory);
            var (executing, actionContext) = Context("POST", "Transactions", "Create", traceIdentifier: "monkey-1");
            var filter = new AuditActionFilter(new AuditTrail(store, TimeProvider.System), TimeProvider.System);

            await filter.OnActionExecutionAsync(executing, () => Task.FromResult(new ActionExecutedContext(actionContext, [], executing.Controller) { Result = new OkResult() }));

            Assert.Equal("monkey-1", Assert.Single(await store.ReadAllAsync(default)).CorrelationId);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task AuditFilter_GetPageView_IsNotAudited()
    {
        var store = new InMemoryAuditStore();
        var (executing, actionContext) = Context("GET", "Inventory", "Index");
        var called = false;

        await new AuditActionFilter(store.Trail(), TimeProvider.System).OnActionExecutionAsync(executing, () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(actionContext, [], executing.Controller) { Result = new OkResult() });
        });

        Assert.True(called);
        Assert.Empty(store.Entries);
    }

    [Fact]
    public async Task AuditFilter_GetMarkedAudited_IsAudited()
    {
        var store = new InMemoryAuditStore();
        var (executing, actionContext) = Context("GET", "Reports", "Index", filters: [new AuditedAttribute()]);

        await new AuditActionFilter(store.Trail(), TimeProvider.System).OnActionExecutionAsync(executing, () =>
            Task.FromResult(new ActionExecutedContext(actionContext, executing.Filters, executing.Controller) { Result = new OkResult() }));

        Assert.Equal("Reports.Index", Assert.Single(store.Entries).TargetReference);
    }

    [Fact]
    public async Task AuditFilter_RecordsRouteIdDurationAndFailureStatus()
    {
        var store = new InMemoryAuditStore();
        var time = new FakeTimeProvider();
        var id = Guid.NewGuid().ToString("D");
        var (executing, actionContext) = Context("POST", "Transactions", "Void", routeId: id);

        await new AuditActionFilter(store.Trail(time), time).OnActionExecutionAsync(executing, () =>
        {
            time.Advance(TimeSpan.FromMilliseconds(250));
            return Task.FromResult(new ActionExecutedContext(actionContext, [], executing.Controller) { Result = new NotFoundResult() });
        });

        var entry = Assert.Single(store.Entries);
        Assert.Equal(id, entry.Metadata["RouteId"]);
        Assert.Equal("250", entry.Metadata["ElapsedMs"]);
        Assert.Equal("404", entry.Metadata["StatusCode"]);
        Assert.Equal("Failed", entry.Outcome);
    }

    [Fact]
    public async Task AuditFilter_NonIdentifierRouteId_IsDropped()
    {
        var store = new InMemoryAuditStore();
        var (executing, actionContext) = Context("POST", "Transactions", "Void", routeId: "4111 1111 1111 1111");

        await new AuditActionFilter(store.Trail(), TimeProvider.System).OnActionExecutionAsync(executing, () =>
            Task.FromResult(new ActionExecutedContext(actionContext, [], executing.Controller) { Result = new OkResult() }));

        Assert.False(Assert.Single(store.Entries).Metadata.ContainsKey("RouteId"));
    }

    private static (ActionExecutingContext Executing, ActionContext ActionContext) Context(string method, string controller, string action, string traceIdentifier = "corr-filter-test", string? routeId = null, List<IFilterMetadata>? filters = null)
    {
        var http = new DefaultHttpContext { TraceIdentifier = traceIdentifier };
        http.Request.Method = method;
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor(), new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary());
        actionContext.RouteData.Values["controller"] = controller;
        actionContext.RouteData.Values["action"] = action;
        if (routeId is not null) actionContext.RouteData.Values["id"] = routeId;
        return (new ActionExecutingContext(actionContext, filters ?? [], new Dictionary<string, object?>(), new object()), actionContext);
    }

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
