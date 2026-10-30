namespace ApexPost.Application.Auditing;

/// <summary>One immutable audit record. Metadata has already passed <see cref="AuditMetadataPolicy"/>.</summary>
public sealed record AuditEntry(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Action,
    string TargetType,
    string TargetReference,
    string Outcome,
    Guid? StaffId,
    string? Role,
    Guid? BranchId,
    string CorrelationId,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>Append-only audit persistence. Entries are never updated or deleted through this port.</summary>
public interface IAuditStore
{
    Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken);
}
