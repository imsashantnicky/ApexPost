using ApexPost.Application.Security;

namespace ApexPost.Application.Auditing;

/// <summary>
/// Builds validated audit entries and appends them to the <see cref="IAuditStore"/>.
/// Callers supply allow-listed metadata only; anything else is rejected before it reaches storage.
/// </summary>
public sealed class AuditTrail
{
    public const string Started = "Started";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Denied = "Denied";

    private readonly IAuditStore store;
    private readonly TimeProvider timeProvider;

    public AuditTrail(IAuditStore store, TimeProvider timeProvider)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public Task RecordAsync(ActorContext? actor, Guid? branchId, string operation, string targetType, string targetReference, string outcome, IReadOnlyDictionary<string, string>? safeMetadata, CancellationToken cancellationToken)
    {
        if (!AuditMetadataPolicy.IsValidOperationName(operation)) throw new ArgumentException("Audit operation name is invalid.", nameof(operation));
        if (string.IsNullOrWhiteSpace(targetType)) throw new ArgumentException("Audit target type is required.", nameof(targetType));
        if (string.IsNullOrWhiteSpace(targetReference)) throw new ArgumentException("Audit target reference is required.", nameof(targetReference));
        if (outcome is not (Started or Succeeded or Failed or Denied)) throw new ArgumentException("Audit outcome is invalid.", nameof(outcome));

        var metadata = safeMetadata ?? new Dictionary<string, string>();
        // CorrelationId is checked by format (as CorrelationIdMiddleware does), never by keyword: "monkey-1" is valid.
        if (metadata.Any(item => item.Key != AuditMetadataPolicy.CorrelationIdKey && !AuditMetadataPolicy.IsAllowed(item.Key, item.Value)))
            throw new ArgumentException("Audit metadata contains unsupported or sensitive values.", nameof(safeMetadata));

        var correlationId = metadata.TryGetValue(AuditMetadataPolicy.CorrelationIdKey, out var suppliedCorrelationId) ? suppliedCorrelationId : actor?.CorrelationId;
        var persistedMetadata = metadata.Where(item => item.Key != AuditMetadataPolicy.CorrelationIdKey).ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        var entry = new AuditEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), operation, targetType, targetReference, outcome,
            actor?.StaffId, actor?.Role, branchId ?? actor?.AssignedBranchId,
            AuditMetadataPolicy.IsValidCorrelationId(correlationId) ? correlationId! : "unknown", persistedMetadata);
        return store.AppendAsync(entry, cancellationToken);
    }

    public Task RecordCrossBranchAccessAsync(ActorContext actor, Guid branchId, string operation, CancellationToken cancellationToken) =>
        RecordAsync(actor, branchId, "Administrator.CrossBranchAccess", "Branch", branchId.ToString("D"), Succeeded,
            new Dictionary<string, string>(StringComparer.Ordinal) { ["Operation"] = operation }, cancellationToken);
}
