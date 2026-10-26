using ApexPost.Infrastructure.Persistence.Json;

namespace ApexPost.Infrastructure.Tests;

public class JsonCollectionStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ApexPostTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MissingAndEmptyFilesReadAsEmptyCollections()
    {
        var store = new JsonCollectionStore<TestItem>(directory, "items.json");
        Assert.Empty(await store.ReadAllAsync(default));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "items.json"), string.Empty);

        Assert.Empty(await store.ReadAllAsync(default));
    }

    [Fact]
    public async Task CreateReadUpdateAndDeletePersistCollection()
    {
        var store = new JsonCollectionStore<TestItem>(directory, "items.json");
        Assert.True(await store.TryAddAsync("one", new("first"), default));
        Assert.False(await store.TryAddAsync("one", new("other"), default));
        Assert.Equal("first", (await store.ReadAllAsync(default))["one"].Name);
        Assert.True(await store.TryReplaceAsync("one", new("updated"), default));
        Assert.Equal("updated", (await store.ReadAllAsync(default))["one"].Name);
        Assert.True(await store.TryDeleteAsync("one", default));
        Assert.Empty(await store.ReadAllAsync(default));
    }

    [Fact]
    public async Task MalformedJsonRaisesExceptionAndPreservesFile()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "items.json");
        await File.WriteAllTextAsync(path, "{ invalid");
        var store = new JsonCollectionStore<TestItem>(directory, "items.json");

        await Assert.ThrowsAsync<JsonPersistenceException>(() => store.ReadAllAsync(default));
        Assert.Equal("{ invalid", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task WriteFailureRaisesPersistenceException()
    {
        Directory.CreateDirectory(directory);
        var blockedDirectory = Path.Combine(directory, "blocked");
        await File.WriteAllTextAsync(blockedDirectory, "not a directory");
        var store = new JsonCollectionStore<TestItem>(blockedDirectory, "items.json");

        await Assert.ThrowsAsync<JsonPersistenceException>(() => store.TryAddAsync("one", new("first"), default));
    }

    [Fact]
    public async Task ConcurrentAddsAreSerializedWithoutLostRecords()
    {
        var store = new JsonCollectionStore<TestItem>(directory, "items.json");
        var writes = Enumerable.Range(0, 20).Select(index => store.TryAddAsync($"key-{index}", new($"item-{index}"), default));

        var results = await Task.WhenAll(writes);
        var items = await store.ReadAllAsync(default);

        Assert.All(results, Assert.True);
        Assert.Equal(20, items.Count);
    }

    [Fact]
    public async Task CancelledOperationStopsBeforeWaitingForCollectionLock()
    {
        var store = new JsonCollectionStore<TestItem>(directory, "cancelled.json");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.ReadAllAsync(cancellation.Token));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }

    public sealed record TestItem(string Name);
}
