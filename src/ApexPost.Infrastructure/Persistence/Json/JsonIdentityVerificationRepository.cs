using ApexPost.Application.IdentityVerification;
using ApexPost.Application.Persistence;
using ApexPost.Domain.Common;
using ApexPost.Domain.IdentityVerification;

namespace ApexPost.Infrastructure.Persistence.Json;

public sealed class JsonIdentityVerificationRepository : IIdentityVerificationRepository
{
    private readonly JsonCollectionStore<Data> store;

    public JsonIdentityVerificationRepository(JsonDataOptions options) => store = new(options.DataDirectory, "identity-verifications.json");

    public async Task<IReadOnlyList<Versioned<IdentityVerificationRecord>>> ListAsync(CancellationToken cancellationToken) =>
        (await store.ReadAllAsync(cancellationToken)).Values.Select(ToDomain).ToArray();

    public async Task<Versioned<IdentityVerificationRecord>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken) =>
        (await store.ReadAllAsync(cancellationToken)).TryGetValue(id.Value.ToString("N"), out var data) ? ToDomain(data) : null;

    public Task<bool> TryAddAsync(IdentityVerificationRecord record, CancellationToken cancellationToken) =>
        store.TryAddAsync(record.Id.Value.ToString("N"), ToData(record, 1), cancellationToken);

    public Task<bool> TryUpdateAsync(IdentityVerificationRecord record, long expectedVersion, CancellationToken cancellationToken) =>
        store.TryReplaceAsync(record.Id.Value.ToString("N"), ToData(record, expectedVersion + 1), data => data.Version == expectedVersion, cancellationToken);

    private static Versioned<IdentityVerificationRecord> ToDomain(Data data) => new(
        IdentityVerificationRecord.Restore(EntityId.From(data.Id), EntityId.From(data.SubjectId), data.Method,
            data.BranchId is Guid branchId ? EntityId.From(branchId) : null, data.Status, data.CompletedAt, data.RejectionReason), data.Version);

    private static Data ToData(IdentityVerificationRecord record, long version) => new(
        record.Id.Value, version, record.SubjectId.Value, record.BranchId?.Value, record.Method, record.Status, record.CompletedAt, record.RejectionReason);

    private sealed record Data(Guid Id, long Version, Guid SubjectId, Guid? BranchId, VerificationMethod Method, VerificationStatus Status, DateTimeOffset? CompletedAt, string? RejectionReason);
}
