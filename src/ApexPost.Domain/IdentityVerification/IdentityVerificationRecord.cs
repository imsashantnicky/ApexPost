using ApexPost.Domain.Common;

namespace ApexPost.Domain.IdentityVerification;

/// <summary>
/// Records a verification outcome without retaining identity-document contents or identifiers.
/// </summary>
public sealed class IdentityVerificationRecord
{
    public IdentityVerificationRecord(EntityId id, EntityId subjectId, VerificationMethod method)
    {
        Id = id ?? throw new DomainException("Verification identity is required.");
        SubjectId = subjectId ?? throw new DomainException("Verification subject identity is required.");

        if (!Enum.IsDefined(method))
        {
            throw new DomainException("Verification method must be recognized.");
        }

        Method = method;
        Status = VerificationStatus.Pending;
    }

    public EntityId Id { get; }

    public EntityId SubjectId { get; }

    public VerificationMethod Method { get; }

    public VerificationStatus Status { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? RejectionReason { get; private set; }

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
