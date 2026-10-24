namespace ApexPost.Infrastructure.Persistence.Json;

/// <summary>Writes operational audit metadata. Secrets and personal-document fields are rejected.</summary>
public sealed class JsonAuditWriter
{
    private static readonly string[] SensitiveKeyFragments = ["password", "token", "secret", "card", "document", "identity", "address", "phone", "email"];
    private readonly JsonCollectionStore<JsonAuditRecord> store;

    public JsonAuditWriter(string dataDirectory)
    {
        store = new JsonCollectionStore<JsonAuditRecord>(dataDirectory, "audit.json");
    }

    public async Task<JsonAuditRecord> WriteAsync(string action, string entityType, string entityId, string? actorId, IReadOnlyDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        Validate(action, nameof(action));
        Validate(entityType, nameof(entityType));
        Validate(entityId, nameof(entityId));
        var safeMetadata = metadata ?? new Dictionary<string, string>();
        if (safeMetadata.Keys.Any(key => SensitiveKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase))))
        {
            throw new ArgumentException("Audit metadata cannot contain sensitive fields.", nameof(metadata));
        }

        var record = new JsonAuditRecord(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, action, entityType, entityId, string.IsNullOrWhiteSpace(actorId) ? null : actorId.Trim(), new Dictionary<string, string>(safeMetadata));
        if (!await store.TryAddAsync(record.Id, record, cancellationToken)) throw new JsonPersistenceException("Audit record identifier collision occurred.", new IOException());
        return record;
    }

    private static void Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Audit value is required.", parameterName);
    }
}
