using System.Collections.Concurrent;
using System.Text.Json;

namespace ApexPost.Infrastructure.Persistence.Json;

/// <summary>
/// File-backed keyed collection storage for a single application process.
/// Each mutation writes a complete replacement file after acquiring a process-local lock.
/// </summary>
public sealed class JsonCollectionStore<T>
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = true
    };

    private readonly string path;
    private readonly SemaphoreSlim fileLock;

    public JsonCollectionStore(string dataDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory)) throw new ArgumentException("Data directory is required.", nameof(dataDirectory));
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName) throw new ArgumentException("File name must not include a path.", nameof(fileName));
        path = Path.GetFullPath(Path.Combine(dataDirectory, fileName));
        fileLock = Locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
    }

    public async Task<IReadOnlyDictionary<string, T>> ReadAllAsync(CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try { return await ReadUnsafeAsync(cancellationToken); }
        finally { fileLock.Release(); }
    }

    public Task<bool> TryAddAsync(string key, T value, CancellationToken cancellationToken) => ChangeAsync(key, value, static (items, itemKey, itemValue) => items.TryAdd(itemKey, itemValue), cancellationToken);

    public Task<bool> TryReplaceAsync(string key, T value, CancellationToken cancellationToken) => ChangeAsync(key, value, static (items, itemKey, itemValue) => { if (!items.ContainsKey(itemKey)) return false; items[itemKey] = itemValue; return true; }, cancellationToken);

    public async Task<bool> TryDeleteAsync(string key, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadUnsafeAsync(cancellationToken);
            if (!items.Remove(key)) return false;
            await WriteUnsafeAsync(items, cancellationToken);
            return true;
        }
        finally { fileLock.Release(); }
    }

    private async Task<bool> ChangeAsync(string key, T value, Func<Dictionary<string, T>, string, T, bool> change, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(value);
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadUnsafeAsync(cancellationToken);
            if (!change(items, key, value)) return false;
            await WriteUnsafeAsync(items, cancellationToken);
            return true;
        }
        finally { fileLock.Release(); }
    }

    private async Task<Dictionary<string, T>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return [];
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            if (stream.Length == 0) return [];
            var items = await JsonSerializer.DeserializeAsync<Dictionary<string, T>>(stream, SerializerOptions, cancellationToken);
            return items ?? throw new JsonPersistenceException($"JSON collection '{path}' contains a null document.", new JsonException());
        }
        catch (JsonPersistenceException) { throw; }
        catch (JsonException exception) { throw new JsonPersistenceException($"JSON collection '{path}' is malformed and was not changed.", exception); }
        catch (IOException exception) { throw new JsonPersistenceException($"JSON collection '{path}' could not be read.", exception); }
    }

    private async Task WriteUnsafeAsync(Dictionary<string, T> items, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)!;
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, items, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, path, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new JsonPersistenceException($"JSON collection '{path}' could not be written. The previous file was preserved.", exception);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Collection key is required.", nameof(key));
    }
}
