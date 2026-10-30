using ApexPost.Application.Results;
using ApexPost.Application.Security;
using ApexPost.Domain.Common;
using ApexPost.Domain.IdentityVerification;

namespace ApexPost.Application.IdentityVerification;

public sealed class IdentityVerificationOperationsUseCase(IIdentityVerificationRepository repository, OperationalAccess access, TimeProvider timeProvider)
{
    public async Task<ApplicationResult<IdentityVerificationResponse>> GetAsync(Guid verificationId, CancellationToken cancellationToken)
    {
        if (verificationId == Guid.Empty) return Invalid();
        var existing = await repository.GetByIdAsync(EntityId.From(verificationId), cancellationToken);
        if (existing is null || existing.Value.BranchId is null) return NotFound();
        var capability = existing.Value.Method == VerificationMethod.ManualSupervisorReview
            ? OperationalCapability.BranchManager
            : OperationalCapability.Counter;
        var authorized = await access.CheckAsync(existing.Value.BranchId.Value, capability, "IdentityVerification.Get", cancellationToken, requireOpenBranch: false);
        return authorized.IsSuccess ? ApplicationResults.Success(ToResponse(existing.Value, existing.Version)) : Failure(authorized.Errors);
    }

    public async Task<ApplicationResult<IdentityVerificationResponse>> StartAsync(StartIdentityVerificationRequest request, CancellationToken cancellationToken)
    {
        if (request is null || request.BranchId == Guid.Empty || request.SubjectId == Guid.Empty)
            return Invalid();
        var authorized = await access.CheckAsync(request.BranchId, OperationalCapability.Counter, "IdentityVerification.Start", cancellationToken);
        if (!authorized.IsSuccess) return Failure(authorized.Errors);
        try
        {
            var record = new IdentityVerificationRecord(EntityId.New(), EntityId.From(request.SubjectId), request.Method, EntityId.From(request.BranchId));
            await access.AuditAsync("IdentityVerification.Start", "IdentityVerification", record.Id.Value.ToString("D"), request.BranchId, "Started", new Dictionary<string, string> { ["Method"] = request.Method.ToString() }, cancellationToken);
            if (!await repository.TryAddAsync(record, cancellationToken)) { await access.AuditAsync("IdentityVerification.Start", "IdentityVerification", record.Id.Value.ToString("D"), request.BranchId, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict(); }
            await access.AuditAsync("IdentityVerification.Start", "IdentityVerification", record.Id.Value.ToString("D"), request.BranchId, "Succeeded", new Dictionary<string, string> { ["Method"] = request.Method.ToString() }, cancellationToken);
            return ApplicationResults.Success(ToResponse(record, 1));
        }
        catch (DomainException exception) { await access.AuditAsync("IdentityVerification.Start", "IdentityVerification", request.BranchId.ToString("D"), request.BranchId, "Failed", new Dictionary<string, string> { ["Status"] = "domain.rule", ["Method"] = request.Method.ToString() }, cancellationToken); return DomainFailure(exception); }
    }

    public async Task<ApplicationResult<IdentityVerificationResponse>> CompleteAsync(CompleteIdentityVerificationRequest request, CancellationToken cancellationToken)
    {
        if (request is null || request.VerificationId == Guid.Empty || request.ExpectedVersion < 1) return Invalid();
        var existing = await repository.GetByIdAsync(EntityId.From(request.VerificationId), cancellationToken);
        if (existing is null || existing.Value.BranchId is null) return NotFound();
        var capability = existing.Value.Method == VerificationMethod.ManualSupervisorReview
            ? OperationalCapability.BranchManager
            : OperationalCapability.Counter;
        var authorized = await access.CheckAsync(existing.Value.BranchId.Value, capability, "IdentityVerification.Complete", cancellationToken);
        if (!authorized.IsSuccess) return Failure(authorized.Errors);
        if (existing.Version != request.ExpectedVersion) { await access.AuditAsync("IdentityVerification.Complete", "IdentityVerification", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict(); }
        try
        {
            if (request.Verified) existing.Value.MarkVerified(timeProvider.GetUtcNow());
            else existing.Value.Reject(request.RejectionReason ?? string.Empty, timeProvider.GetUtcNow());
            await access.AuditAsync("IdentityVerification.Complete", "IdentityVerification", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId?.Value, "Started", new Dictionary<string, string> { ["Verified"] = request.Verified.ToString() }, cancellationToken);
            if (!await repository.TryUpdateAsync(existing.Value, request.ExpectedVersion, cancellationToken)) { await access.AuditAsync("IdentityVerification.Complete", "IdentityVerification", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId?.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict(); }
            await access.AuditAsync("IdentityVerification.Complete", "IdentityVerification", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId?.Value, "Succeeded", new Dictionary<string, string> { ["Status"] = existing.Value.Status.ToString(), ["Verified"] = request.Verified.ToString() }, cancellationToken);
            return ApplicationResults.Success(ToResponse(existing.Value, request.ExpectedVersion + 1));
        }
        catch (DomainException exception) { await access.AuditAsync("IdentityVerification.Complete", "IdentityVerification", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId?.Value, "Failed", new Dictionary<string, string> { ["Status"] = "domain.rule", ["Verified"] = request.Verified.ToString() }, cancellationToken); return DomainFailure(exception); }
    }

    private static IdentityVerificationResponse ToResponse(IdentityVerificationRecord record, long version) =>
        new(record.Id.Value, version, record.SubjectId.Value, record.BranchId?.Value ?? Guid.Empty, record.Method, record.Status, record.CompletedAt, record.RejectionReason);
    private static ApplicationResult<IdentityVerificationResponse> Invalid() => ApplicationResults.Failure<IdentityVerificationResponse>([new("request.invalid", "The request is invalid.")]);
    private static ApplicationResult<IdentityVerificationResponse> NotFound() => ApplicationResults.Failure<IdentityVerificationResponse>([new("resource.not-found", "The requested verification was not found.")]);
    private static ApplicationResult<IdentityVerificationResponse> Conflict() => ApplicationResults.Failure<IdentityVerificationResponse>([new("concurrency.conflict", "The verification changed before it could be saved.")]);
    private static ApplicationResult<IdentityVerificationResponse> DomainFailure(DomainException exception) => ApplicationResults.Failure<IdentityVerificationResponse>([new("domain.rule", exception.Message)]);
    private static ApplicationResult<IdentityVerificationResponse> Failure(IReadOnlyList<ValidationError> errors) => ApplicationResults.Failure<IdentityVerificationResponse>(errors);
}
