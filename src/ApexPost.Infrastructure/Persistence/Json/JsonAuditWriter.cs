using ApexPost.Application.Security;

namespace ApexPost.Infrastructure.Persistence.Json;

/// <summary>Writes operational audit metadata. Secrets and personal-document fields are rejected.</summary>
public sealed class JsonAuditWriter : IAccessAuditWriter
{
    private static readonly string[] SensitiveKeyFragments = ["password", "passwd", "token", "secret", "card", "cvv", "credential", "payment", "document", "identity", "passport", "ssn", "address", "phone", "email", "auth", "session", "key"];
    private readonly JsonCollectionStore<JsonAuditRecord> store;

    public JsonAuditWriter(string dataDirectory)
    {
        store = new JsonCollectionStore<JsonAuditRecord>(dataDirectory, "audit.json");
    }

    public async Task WriteOperationAsync(ActorContext? actor, Guid? branchId, string operation, string targetType, string targetReference, string outcome, IReadOnlyDictionary<string, string>? safeMetadata, CancellationToken cancellationToken)
    {
        Validate(operation, nameof(operation));
        Validate(targetType, nameof(targetType));
        Validate(targetReference, nameof(targetReference));
        if (outcome is not ("Succeeded" or "Failed" or "Denied" or "Started")) throw new ArgumentException("Audit outcome is invalid.", nameof(outcome));
        var metadata = safeMetadata ?? new Dictionary<string, string>();
        if (metadata.Keys.Any(key => !SafeMetadataKeys.Contains(key, StringComparer.Ordinal)) || metadata.Values.Any(ContainsSensitiveText))
            throw new ArgumentException("Audit metadata contains unsupported or sensitive values.", nameof(safeMetadata));
        var correlationId = metadata.TryGetValue("CorrelationId", out var suppliedCorrelationId)
            ? suppliedCorrelationId
            : actor?.CorrelationId;
        var persistedMetadata = metadata.Where(item => !string.Equals(item.Key, "CorrelationId", StringComparison.Ordinal)).ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var record = new JsonAuditRecord(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, operation, targetType,
            targetReference, actor?.StaffId.ToString("D"), persistedMetadata, actor?.Role,
            branchId ?? actor?.AssignedBranchId, outcome, string.IsNullOrWhiteSpace(correlationId) ? "unknown" : correlationId);
        if (!await store.TryAddAsync(record.Id, record, cancellationToken)) throw new JsonPersistenceException("Audit record identifier collision occurred.", new IOException());
    }

    public async Task WriteAdministratorCrossBranchAccessAsync(ActorContext actor, Guid branchId, string operation, CancellationToken cancellationToken)
    {
        await WriteOperationAsync(actor, branchId, "Administrator.CrossBranchAccess", "Branch", branchId.ToString("D"), "Succeeded",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Operation"] = operation }, cancellationToken);
    }

    private static readonly string[] SafeMetadataKeys = ["Operation", "Status", "Quantity", "Version", "Method", "Verified", "HttpMethod", "StatusCode", "ElapsedMs", "CorrelationId", "StaffRoles", "CounterCount"];

    private static void Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Audit value is required.", parameterName);
    }

    private static bool ContainsSensitiveText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return SensitiveKeyFragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
