using ApexPost.Domain.Common;

namespace ApexPost.Domain.IdentityVerification;

/// <summary>
/// Records a verification outcome without retaining identity-document contents or identifiers.
/// </summary>
public sealed class IdentityVerificationRecord
{
    public IdentityVerificationRecord(EntityId id, EntityId subjectId, VerificationMethod method)
        : this(id, subjectId, method, null)
    {
    }

    public IdentityVerificationRecord(EntityId id, EntityId subjectId, VerificationMethod method, EntityId? branchId)
    {
        Id = id ?? throw new DomainException("Verification identity is required.");
        SubjectId = subjectId ?? throw new DomainException("Verification subject identity is required.");
        BranchId = branchId;

        if (!Enum.IsDefined(method))
        {
            throw new DomainException("Verification method must be recognized.");
        }

        Method = method;
        Status = VerificationStatus.Pending;
    }

    public EntityId Id { get; }

    public EntityId SubjectId { get; }

    public EntityId? BranchId { get; }

    public VerificationMethod Method { get; }

    public VerificationStatus Status { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? RejectionReason { get; private set; }

    public static IdentityVerificationRecord Restore(EntityId id, EntityId subjectId, VerificationMethod method, EntityId? branchId, VerificationStatus status, DateTimeOffset? completedAt, string? rejectionReason)
    {
        var record = new IdentityVerificationRecord(id, subjectId, method, branchId);
        if (status == VerificationStatus.Pending)
        {
            if (completedAt is not null || rejectionReason is not null) throw new DomainException("Pending verification cannot contain completion data.");
            return record;
        }
        if (completedAt is null) throw new DomainException("Completed verification requires a completion time.");
        if (status == VerificationStatus.Verified)
        {
            if (rejectionReason is not null) throw new DomainException("Verified identity record cannot contain a rejection reason.");
            record.MarkVerified(completedAt.Value);
            return record;
        }
        if (status == VerificationStatus.Rejected)
        {
            record.Reject(rejectionReason ?? string.Empty, completedAt.Value);
            return record;
        }
        throw new DomainException("Verification status must be recognized.");
    }

    public void MarkVerified(DateTimeOffset completedAt)
    {
        EnsurePending();
        EnsureCompletionTime(completedAt);
        Status = VerificationStatus.Verified;
        CompletedAt = completedAt;
    }

    public void Reject(string reason, DateTimeOffset completedAt)
    {
        EnsurePending();
        EnsureCompletionTime(completedAt);
        var normalizedReason = reason?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > 250)
        {
            throw new DomainException("Verification rejection reason is required and must not exceed 250 characters.");
        }

        Status = VerificationStatus.Rejected;
        RejectionReason = normalizedReason;
        CompletedAt = completedAt;
    }

    private void EnsurePending()
    {
        if (Status != VerificationStatus.Pending)
        {
            throw new DomainException("Only a pending verification can be completed.");
        }
    }

    private static void EnsureCompletionTime(DateTimeOffset completedAt)
    {
        if (completedAt == default)
        {
            throw new DomainException("Verification completion time is required.");
        }
    }
}
