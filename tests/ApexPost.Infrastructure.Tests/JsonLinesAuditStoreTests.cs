using System.Text.Json;
using ApexPost.Application.Auditing;
using ApexPost.Application.Security;
using ApexPost.Infrastructure.Persistence.Json;
using Microsoft.Extensions.Time.Testing;

namespace ApexPost.Infrastructure.Tests;

public class JsonLinesAuditStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 9, 30, 0, TimeSpan.Zero);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ApexPostAuditTests", Guid.NewGuid().ToString("N"));

    private string AuditPath => Path.Combine(directory, JsonLinesAuditStore.FileName);

    [Fact]
    public async Task AuditTrailStoresTrustedContextAndSafeFields()
    {
        var actor = new ActorContext(Guid.Parse("11111111-1111-1111-1111-111111111111"), "BranchManager", Guid.Parse("22222222-2222-2222-2222-222222222222"), "corr-synthetic-01");

        await Trail().RecordAsync(actor, actor.AssignedBranchId, "Transaction.Refund", "RetailTransaction", "33333333-3333-3333-3333-333333333333", "Succeeded", new Dictionary<string, string> { ["Status"] = "Reversed", ["Version"] = "3" }, default);

        var stored = Assert.Single(await Store().ReadAllAsync(default));
        Assert.Equal("Transaction.Refund", stored.Action);
        Assert.Equal(actor.StaffId, stored.StaffId);
        Assert.Equal("BranchManager", stored.Role);
        Assert.Equal(actor.AssignedBranchId, stored.BranchId);
        Assert.Equal("Succeeded", stored.Outcome);
        Assert.Equal(actor.CorrelationId, stored.CorrelationId);
        Assert.Equal(Now, stored.OccurredAt);
        Assert.Equal("Reversed", stored.Metadata["Status"]);
    }

    [Fact]
    public async Task EachEntryIsOneJsonObjectPerLineAndEarlierLinesAreNeverRewritten()
    {
        var trail = Trail();
        await trail.RecordAsync(null, null, "Inventory.Adjust", "InventoryItem", "first", "Succeeded", null, default);
        var afterFirst = await File.ReadAllBytesAsync(AuditPath);

        await trail.RecordAsync(null, null, "Inventory.Adjust", "InventoryItem", "second", "Succeeded", null, default);

        var content = await File.ReadAllBytesAsync(AuditPath);
        Assert.Equal(afterFirst, content.Take(afterFirst.Length));
        var lines = (await File.ReadAllLinesAsync(AuditPath)).Where(line => line.Length > 0).ToArray();
        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(line).RootElement.ValueKind));
    }

    [Fact]
    public async Task ExistingContentIsPreservedEvenWhenItIsNotValidJson()
    {
        Directory.CreateDirectory(directory);
        const string existing = "{ legacy partial line\n";
        await File.WriteAllTextAsync(AuditPath, existing);

        await Trail().RecordAsync(null, null, "MoneyOrder.Issue", "MoneyOrder", "synthetic-target", "Succeeded", null, default);

        Assert.StartsWith(existing, await File.ReadAllTextAsync(AuditPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentWritesAreAllAppendedAndCancellationIsPropagated()
    {
        var trail = Trail();
        await Task.WhenAll(Enumerable.Range(0, 80).Select(index =>
            trail.RecordAsync(null, null, "Inventory.Adjust", "InventoryItem", index.ToString(System.Globalization.CultureInfo.InvariantCulture), "Succeeded", null, default)));

        Assert.Equal(80, (await Store().ReadAllAsync(default)).Count);
        using var canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => trail.RecordAsync(null, null, "Shipment.Book", "Shipment", "sample", "Succeeded", null, canceled.Token));
        Assert.Equal(80, (await Store().ReadAllAsync(default)).Count);
    }

    [Theory]
    [InlineData("PaymentSecret", "never-store")]
    [InlineData("DocumentNumber", "SYNTHETIC")]
    [InlineData("Status", "api-key-should-not-be-here")]
    [InlineData("Quantity", "4111111111111111")]
    [InlineData("RouteId", "free text")]
    public async Task SensitiveOrUnapprovedMetadataIsRejectedBeforeAnythingIsWritten(string key, string value)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Trail().RecordAsync(null, null, "MoneyOrder.Issue", "MoneyOrder", "sample", "Succeeded", new Dictionary<string, string> { [key] = value }, default));
        Assert.False(File.Exists(AuditPath));
    }

    [Theory]
    [InlineData("Transaction.StartPayment")]
    [InlineData("Transaction.PaymentConfirmed")]
    [InlineData("Transaction.RetryPayment")]
    [InlineData("IdentityVerification.Start")]
    [InlineData("IdentityVerification.Complete")]
    public async Task CrossBranchAudit_OperationNameContainingSensitiveWord_IsRecorded(string operation)
    {
        var actor = new ActorContext(Guid.NewGuid(), "Administrator", Guid.NewGuid(), "corr-cross-branch");

        await Trail().RecordCrossBranchAccessAsync(actor, Guid.NewGuid(), operation, default);

        Assert.Equal(operation, Assert.Single(await Store().ReadAllAsync(default)).Metadata["Operation"]);
    }

    [Theory]
    [InlineData("password=hunter2")]
    [InlineData("Transaction")]
    [InlineData("Transaction.Start Payment")]
    public async Task CrossBranchAudit_MalformedOperationName_IsRejected(string operation)
    {
        var actor = new ActorContext(Guid.NewGuid(), "Administrator", Guid.NewGuid(), "corr-cross-branch");

        await Assert.ThrowsAsync<ArgumentException>(() => Trail().RecordCrossBranchAccessAsync(actor, Guid.NewGuid(), operation, default));
        Assert.False(File.Exists(AuditPath));
    }

    [Fact]
    public async Task StartPaymentSucceeded_PaymentPendingStatus_IsRecorded()
    {
        await Trail().RecordAsync(null, null, "Transaction.StartPayment", "RetailTransaction", "tx-1", "Succeeded", new Dictionary<string, string> { ["Status"] = "PaymentPending", ["Version"] = "2" }, default);

        Assert.Equal("PaymentPending", Assert.Single(await Store().ReadAllAsync(default)).Metadata["Status"]);
    }

    [Fact]
    public async Task CorrelationIdContainingSensitiveWord_IsRecorded()
    {
        await Trail().RecordAsync(null, null, "Mvc.ActionCompleted", "MvcAction", "Home.Index", "Succeeded", new Dictionary<string, string> { ["CorrelationId"] = "monkey-1" }, default);

        Assert.Equal("monkey-1", Assert.Single(await Store().ReadAllAsync(default)).CorrelationId);
    }

    private JsonLinesAuditStore Store() => new(directory);

    private AuditTrail Trail() => new(Store(), new FakeTimeProvider(Now));

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }
}
