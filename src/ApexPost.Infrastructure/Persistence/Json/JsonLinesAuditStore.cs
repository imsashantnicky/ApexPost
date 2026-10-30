using System.Collections.Concurrent;
using System.Text.Json;
using ApexPost.Application.Auditing;

namespace ApexPost.Infrastructure.Persistence.Json;

/// <summary>
/// Append-only JSON Lines audit log: one JSON object per line in <c>audit.jsonl</c>. The file is only ever
/// opened with <see cref="FileMode.Append"/>, so existing entries are never rewritten. Writes are serialized
/// per file within the process; this is not tamper-proof and does not coordinate multiple processes.
/// </summary>
public sealed class JsonLinesAuditStore : IAuditStore
{
    public const string FileName = "audit.jsonl";

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly string path;
    private readonly SemaphoreSlim fileLock;

    public JsonLinesAuditStore(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory)) throw new ArgumentException("Data directory is required.", nameof(dataDirectory));
        path = Path.GetFullPath(Path.Combine(dataDirectory, FileName));
        fileLock = Locks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
    }

    public async Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var json = JsonSerializer.SerializeToUtf8Bytes(entry, SerializerOptions);
        var line = new byte[json.Length + 1];
        json.CopyTo(line, 0);
        line[^1] = (byte)'\n';

        await fileLock.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous);
            // Once started, the line is written in full: cancelling half-way would leave a torn record.
            await stream.WriteAsync(line, CancellationToken.None);
            await stream.FlushAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new JsonPersistenceException($"Audit log '{path}' could not be appended.", exception);
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <summary>Reads every entry in append order. Intended for operators and tests, not request handling.</summary>
    public async Task<IReadOnlyList<AuditEntry>> ReadAllAsync(CancellationToken cancellationToken)
    {
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(path)) return [];
            var entries = new List<AuditEntry>();
            foreach (var line in await File.ReadAllLinesAsync(path, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                entries.Add(JsonSerializer.Deserialize<AuditEntry>(line, SerializerOptions)
                    ?? throw new JsonPersistenceException($"Audit log '{path}' contains a null entry.", new JsonException()));
            }
            return entries;
        }
        catch (JsonException exception)
        {
            throw new JsonPersistenceException($"Audit log '{path}' contains a malformed entry.", exception);
        }
        finally
        {
            fileLock.Release();
        }
    }
}
