using ApexPost.Infrastructure.Persistence.Json;

namespace ApexPost.Infrastructure.Tests;

public class JsonAuditAndRecoveryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ApexPostAuditTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AuditWriterStoresOperationalMetadata()
    {
        var writer = new JsonAuditWriter(directory);
        var record = await writer.WriteAsync("branch.suspended", "Branch", "branch-1", "staff-1", new Dictionary<string, string> { ["reasonCode"] = "maintenance" }, default);
        var store = new JsonCollectionStore<JsonAuditRecord>(directory, "audit.json");

        var persisted = Assert.Single(await store.ReadAllAsync(default)).Value;
        Assert.Equal(record.Id, persisted.Id);
        Assert.Equal("maintenance", persisted.Metadata["reasonCode"]);
    }

    [Fact]
    public async Task AuditWriterRejectsSensitiveMetadata()
    {
        var writer = new JsonAuditWriter(directory);

        await Assert.ThrowsAsync<ArgumentException>(() => writer.WriteAsync("payment.started", "Transaction", "tx-1", null, new Dictionary<string, string> { ["cardNumber"] = "never-store" }, default));
        Assert.False(File.Exists(Path.Combine(directory, "audit.json")));
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
