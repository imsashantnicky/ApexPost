using ApexPost.Domain.IdentityVerification;

namespace ApexPost.Application.IdentityVerification;

public sealed record StartIdentityVerificationRequest(Guid BranchId, Guid SubjectId, VerificationMethod Method);
public sealed record CompleteIdentityVerificationRequest(Guid VerificationId, long ExpectedVersion, bool Verified, string? RejectionReason);
public sealed record IdentityVerificationResponse(Guid Id, long Version, Guid SubjectId, Guid BranchId, VerificationMethod Method, VerificationStatus Status, DateTimeOffset? CompletedAt, string? RejectionReason);
