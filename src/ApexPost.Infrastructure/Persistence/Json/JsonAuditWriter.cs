using ApexPost.Application.Auditing;
using ApexPost.Application.Security;

namespace ApexPost.Infrastructure.Persistence.Json;

/// <summary>Writes operational audit metadata. Secrets and personal-document fields are rejected.</summary>
public sealed class JsonAuditWriter : IAccessAuditWriter
{
    private readonly JsonCollectionStore<JsonAuditRecord> store;

    public JsonAuditWriter(string dataDirectory)
    {
        store = new JsonCollectionStore<JsonAuditRecord>(dataDirectory, "audit.json");
    }

    public async Task WriteOperationAsync(ActorContext? actor, Guid? branchId, string operation, string targetType, string targetReference, string outcome, IReadOnlyDictionary<string, string>? safeMetadata, CancellationToken cancellationToken)
    {
        if (!AuditMetadataPolicy.IsValidOperationName(operation)) throw new ArgumentException("Audit operation name is invalid.", nameof(operation));
        Validate(targetType, nameof(targetType));
        Validate(targetReference, nameof(targetReference));
        if (outcome is not ("Succeeded" or "Failed" or "Denied" or "Started")) throw new ArgumentException("Audit outcome is invalid.", nameof(outcome));
        var metadata = safeMetadata ?? new Dictionary<string, string>();
        // CorrelationId is checked by format (as CorrelationIdMiddleware does), never by keyword: "monkey-1" is valid.
        if (metadata.Any(item => item.Key != AuditMetadataPolicy.CorrelationIdKey && !AuditMetadataPolicy.IsAllowed(item.Key, item.Value)))
            throw new ArgumentException("Audit metadata contains unsupported or sensitive values.", nameof(safeMetadata));
        var correlationId = metadata.TryGetValue(AuditMetadataPolicy.CorrelationIdKey, out var suppliedCorrelationId)
            ? suppliedCorrelationId
            : actor?.CorrelationId;
        var persistedMetadata = metadata.Where(item => item.Key != AuditMetadataPolicy.CorrelationIdKey).ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var record = new JsonAuditRecord(Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow, operation, targetType,
            targetReference, actor?.StaffId.ToString("D"), persistedMetadata, actor?.Role,
            branchId ?? actor?.AssignedBranchId, outcome, AuditMetadataPolicy.IsValidCorrelationId(correlationId) ? correlationId! : "unknown");
        if (!await store.TryAddAsync(record.Id, record, cancellationToken)) throw new JsonPersistenceException("Audit record identifier collision occurred.", new IOException());
    }

    public async Task WriteAdministratorCrossBranchAccessAsync(ActorContext actor, Guid branchId, string operation, CancellationToken cancellationToken)
    {
        await WriteOperationAsync(actor, branchId, "Administrator.CrossBranchAccess", "Branch", branchId.ToString("D"), "Succeeded",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Operation"] = operation }, cancellationToken);
    }

    private static void Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Audit value is required.", parameterName);
    }
}
