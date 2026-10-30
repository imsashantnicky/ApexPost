using ApexPost.Infrastructure.Persistence.Json;
using ApexPost.Application.Security;

namespace ApexPost.Infrastructure.Tests;

public class JsonAuditAndRecoveryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ApexPostAuditTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AuditWriterStoresOperationalMetadata()
    {
        var writer = new JsonAuditWriter(directory);
        var actor = new ActorContext(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Administrator", Guid.Parse("22222222-2222-2222-2222-222222222222"), "audit-test-correlation");
        await writer.WriteOperationAsync(actor, actor.AssignedBranchId, "Branch.Deactivate", "Branch", "33333333-3333-3333-3333-333333333333", "Succeeded", new Dictionary<string, string> { ["Status"] = "Suspended" }, default);
        var store = new JsonCollectionStore<JsonAuditRecord>(directory, "audit.json");

        var persisted = Assert.Single(await store.ReadAllAsync(default)).Value;
        Assert.Equal("Branch.Deactivate", persisted.Action);
        Assert.Equal("Administrator", persisted.ActorRole);
        Assert.Equal(actor.CorrelationId, persisted.CorrelationId);
        Assert.Equal("Suspended", persisted.Metadata["Status"]);
    }

    [Fact]
    public async Task AuditWriterRejectsSensitiveMetadata()
    {
        var writer = new JsonAuditWriter(directory);

        await Assert.ThrowsAsync<ArgumentException>(() => writer.WriteOperationAsync(null, null, "Transaction.StartPayment", "RetailTransaction", "tx-1", "Succeeded", new Dictionary<string, string> { ["PaymentSecret"] = "never-store" }, default));
        Assert.False(File.Exists(Path.Combine(directory, "audit.json")));
    }

    [Theory]
    [InlineData("Transaction.StartPayment")]
    [InlineData("Transaction.PaymentConfirmed")]
    [InlineData("Transaction.RetryPayment")]
    [InlineData("IdentityVerification.Start")]
    [InlineData("IdentityVerification.Complete")]
    public async Task CrossBranchAudit_OperationNameContainingSensitiveWord_IsRecorded(string operation)
    {
        var writer = new JsonAuditWriter(directory);
        var actor = new ActorContext(Guid.NewGuid(), "Administrator", Guid.NewGuid(), "corr-cross-branch");

        await writer.WriteAdministratorCrossBranchAccessAsync(actor, Guid.NewGuid(), operation, default);

        var stored = Assert.Single(await new JsonCollectionStore<JsonAuditRecord>(directory, "audit.json").ReadAllAsync(default)).Value;
        Assert.Equal(operation, stored.Metadata["Operation"]);
    }

    [Theory]
    [InlineData("password=hunter2")]
    [InlineData("Transaction")]
    [InlineData("Transaction.Start Payment")]
    public async Task CrossBranchAudit_MalformedOperationName_IsRejected(string operation)
    {
        var writer = new JsonAuditWriter(directory);
        var actor = new ActorContext(Guid.NewGuid(), "Administrator", Guid.NewGuid(), "corr-cross-branch");

        await Assert.ThrowsAsync<ArgumentException>(() => writer.WriteAdministratorCrossBranchAccessAsync(actor, Guid.NewGuid(), operation, default));
        Assert.False(File.Exists(Path.Combine(directory, "audit.json")));
    }

    [Fact]
    public async Task StartPaymentSucceeded_PaymentPendingStatus_IsRecorded()
    {
        var writer = new JsonAuditWriter(directory);

        await writer.WriteOperationAsync(null, null, "Transaction.StartPayment", "RetailTransaction", "tx-1", "Succeeded", new Dictionary<string, string> { ["Status"] = "PaymentPending", ["Version"] = "2" }, default);

        Assert.Equal("PaymentPending", Assert.Single(await new JsonCollectionStore<JsonAuditRecord>(directory, "audit.json").ReadAllAsync(default)).Value.Metadata["Status"]);
    }

    [Fact]
    public async Task CorrelationIdContainingSensitiveWord_IsRecorded()
    {
        var writer = new JsonAuditWriter(directory);

        await writer.WriteOperationAsync(null, null, "Mvc.ActionCompleted", "MvcAction", "Home.Index", "Succeeded", new Dictionary<string, string> { ["CorrelationId"] = "monkey-1" }, default);

        Assert.Equal("monkey-1", Assert.Single(await new JsonCollectionStore<JsonAuditRecord>(directory, "audit.json").ReadAllAsync(default)).Value.CorrelationId);
    }

    [Fact]
    public async Task OperationAuditContainsTrustedContextAndSafeFieldsOnly()
    {
        var writer = new JsonAuditWriter(directory);
        var actor = new ActorContext(Guid.Parse("11111111-1111-1111-1111-111111111111"), "BranchManager", Guid.Parse("22222222-2222-2222-2222-222222222222"), "corr-synthetic-01");
        await writer.WriteOperationAsync(actor, actor.AssignedBranchId, "Transaction.Refund", "RetailTransaction", "33333333-3333-3333-3333-333333333333", "Succeeded", new Dictionary<string, string> { ["Status"] = "Reversed", ["Version"] = "3" }, default);

        var stored = Assert.Single(await new JsonCollectionStore<JsonAuditRecord>(directory, "audit.json").ReadAllAsync(default)).Value;
        Assert.Equal(actor.StaffId.ToString("D"), stored.ActorId);
        Assert.Equal("BranchManager", stored.ActorRole);
        Assert.Equal(actor.AssignedBranchId, stored.BranchId);
        Assert.Equal("Succeeded", stored.Outcome);
        Assert.Equal(actor.CorrelationId, stored.CorrelationId);
        Assert.Equal(DateTimeOffset.UtcNow.Offset, stored.OccurredAt.Offset);
        Assert.Equal("Reversed", stored.Metadata["Status"]);
    }

    [Fact]
    public async Task ConcurrentAuditWritesRemainAppendOnlyAndCancellationIsPropagated()
    {
        var writer = new JsonAuditWriter(directory);
        var writes = Enumerable.Range(0, 80).Select(index => writer.WriteOperationAsync(null, null, "Inventory.Adjust", "InventoryItem", index.ToString(System.Globalization.CultureInfo.InvariantCulture), "Succeeded", null, default));
        await Task.WhenAll(writes);

        var store = new JsonCollectionStore<JsonAuditRecord>(directory, "audit.json");
        Assert.Equal(80, (await store.ReadAllAsync(default)).Count);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.WriteOperationAsync(null, null, "Shipment.Book", "Shipment", "sample", "Succeeded", null, canceled.Token));
        Assert.Equal(80, (await store.ReadAllAsync(default)).Count);
    }

    [Fact]
    public async Task OperationAuditRejectsSensitiveOrUnapprovedMetadata()
    {
        var writer = new JsonAuditWriter(directory);
        await Assert.ThrowsAsync<ArgumentException>(() => writer.WriteOperationAsync(null, null, "IdentityVerification.Complete", "IdentityVerification", "sample", "Succeeded", new Dictionary<string, string> { ["DocumentNumber"] = "SYNTHETIC" }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => writer.WriteOperationAsync(null, null, "MoneyOrder.Issue", "MoneyOrder", "sample", "Succeeded", new Dictionary<string, string> { ["Status"] = "api-key-should-not-be-here" }, default));
        Assert.False(File.Exists(Path.Combine(directory, "audit.json")));
    }

    [Fact]
    public async Task MalformedAuditFileIsPreservedAndAuditFailurePropagates()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "audit.json");
        const string malformed = "{ malformed audit data";
        await File.WriteAllTextAsync(path, malformed);
        var writer = new JsonAuditWriter(directory);

        await Assert.ThrowsAsync<JsonPersistenceException>(() => writer.WriteOperationAsync(null, null, "MoneyOrder.Issue", "MoneyOrder", "synthetic-target", "Succeeded", null, default));

        Assert.Equal(malformed, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task BackupAndExplicitRecoveryRestoreValidPreviousData()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "items.json");
        const string original = "{\"one\":{\"name\":\"first\"}}";
        await File.WriteAllTextAsync(path, original);
        await JsonBackupRecovery.CreateBackupAsync(path, default);
        await File.WriteAllTextAsync(path, "{\"two\":{\"name\":\"second\"}}");

        await JsonBackupRecovery.RestoreBackupAsync(path, default);

        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task MalformedBackupDoesNotReplaceCurrentData()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "items.json");
        const string current = "{\"current\":true}";
        await File.WriteAllTextAsync(path, current);
        await File.WriteAllTextAsync($"{path}.bak", "{ invalid");

        await Assert.ThrowsAsync<JsonPersistenceException>(() => JsonBackupRecovery.RestoreBackupAsync(path, default));

        Assert.Equal(current, await File.ReadAllTextAsync(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }
}
