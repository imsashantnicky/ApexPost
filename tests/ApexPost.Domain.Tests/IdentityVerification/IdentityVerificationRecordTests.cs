using ApexPost.Domain.Common;
using ApexPost.Domain.IdentityVerification;

namespace ApexPost.Domain.Tests.IdentityVerification;

public class IdentityVerificationRecordTests
{
    [Fact]
    public void ConstructorCreatesPendingVerificationWithoutDocumentData()
    {
        var verification = new IdentityVerificationRecord(
            EntityId.New(),
            EntityId.New(),
            VerificationMethod.InPersonReview);

        Assert.Equal(VerificationStatus.Pending, verification.Status);
        Assert.Null(verification.CompletedAt);
        Assert.Null(verification.RejectionReason);
    }

    [Fact]
    public void PendingVerificationCanBeMarkedVerified()
    {
        var verification = CreateVerification();
        var completedAt = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

        verification.MarkVerified(completedAt);

        Assert.Equal(VerificationStatus.Verified, verification.Status);
        Assert.Equal(completedAt, verification.CompletedAt);
    }

    [Fact]
    public void RejectionRequiresReasonAndIsTerminal()
    {
        var verification = CreateVerification();

        Assert.Throws<DomainException>(() => verification.Reject(" ", DateTimeOffset.UtcNow));
        verification.Reject("Document could not be verified.", DateTimeOffset.UtcNow);

        Assert.Equal(VerificationStatus.Rejected, verification.Status);
        Assert.Throws<DomainException>(() => verification.MarkVerified(DateTimeOffset.UtcNow));
    }

    private static IdentityVerificationRecord CreateVerification()
    {
        return new IdentityVerificationRecord(
            EntityId.New(),
            EntityId.New(),
            VerificationMethod.ManualSupervisorReview);
    }
}
