namespace ApexPost.Infrastructure.Persistence.Json;

public sealed record JsonAuditRecord(string Id, DateTimeOffset OccurredAt, string Action, string EntityType, string EntityId, string? ActorId, IReadOnlyDictionary<string, string> Metadata);
