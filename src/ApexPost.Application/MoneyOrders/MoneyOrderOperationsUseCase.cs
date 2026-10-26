using ApexPost.Application.Results;
using ApexPost.Domain.Common;
using ApexPost.Domain.MoneyOrders;
using ApexPost.Application.Security;
using ApexPost.Application.IdentityVerification;
using ApexPost.Domain.IdentityVerification;

namespace ApexPost.Application.MoneyOrders;

public sealed class MoneyOrderOperationsUseCase
{
    private readonly IMoneyOrderRepository repository;
    private readonly OperationalAccess access;
    private readonly IIdentityVerificationRepository verifications;
    private readonly MoneyOrderPolicy policy;
    public MoneyOrderOperationsUseCase(IMoneyOrderRepository repository, OperationalAccess access, IIdentityVerificationRepository verifications, MoneyOrderPolicy policy) { this.repository = repository ?? throw new ArgumentNullException(nameof(repository)); this.access = access; this.verifications = verifications ?? throw new ArgumentNullException(nameof(verifications)); this.policy = policy ?? throw new ArgumentNullException(nameof(policy)); }

    public async Task<ApplicationResult<MoneyOrderResponse>> IssueAsync(IssueMoneyOrderRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.OriginatingBranchId, out var branchId)) return Invalid<MoneyOrderResponse>();
        var authorized = await access.CheckAsync(branchId.Value, OperationalCapability.Counter, "MoneyOrder.Issue", cancellationToken);
        if (!authorized.IsSuccess) return Failure<MoneyOrderResponse>(authorized.Errors);
        EntityId? verifiedIdentityId = null;
        if (request.Amount > policy.IdentityVerificationThreshold)
        {
            if (request.IdentityVerificationId is not Guid verificationId || verificationId == Guid.Empty)
            {
                await access.AuditAsync("MoneyOrder.Issue", "MoneyOrder", branchId.Value.ToString("D"), branchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "verification-required" }, cancellationToken);
                return ApplicationResults.Failure<MoneyOrderResponse>([new("identity-verification.required", "A successful identity verification is required for this money order.")]);
            }
            var verification = await verifications.GetByIdAsync(EntityId.From(verificationId), cancellationToken);
            if (verification is null || verification.Value.Status != VerificationStatus.Verified || verification.Value.BranchId?.Value != branchId.Value)
            {
                await access.AuditAsync("MoneyOrder.Issue", "MoneyOrder", branchId.Value.ToString("D"), branchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "verification-invalid" }, cancellationToken);
                return ApplicationResults.Failure<MoneyOrderResponse>([new("identity-verification.invalid", "A successful identity verification is required for this money order.")]);
            }
            verifiedIdentityId = verification.Value.Id;
        }
        try
        {
            var moneyOrder = new MoneyOrder(EntityId.New(), MoneyOrderNumber.From(request.OrderNumber), new MoneyOrderAmount(request.Amount, request.CurrencyCode), branchId, new MoneyOrderBeneficiary(request.BeneficiaryName, request.BeneficiaryReference), verifiedIdentityId);
            await access.AuditAsync("MoneyOrder.Issue", "MoneyOrder", moneyOrder.Id.Value.ToString("D"), branchId.Value, "Started", new Dictionary<string, string> { ["Verified"] = (verifiedIdentityId is not null).ToString() }, cancellationToken);
            if (!await repository.TryAddAsync(moneyOrder, cancellationToken)) { await access.AuditAsync("MoneyOrder.Issue", "MoneyOrder", moneyOrder.Id.Value.ToString("D"), branchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "identifier.duplicate" }, cancellationToken); return Duplicate<MoneyOrderResponse>("orderNumber"); }
            await access.AuditAsync("MoneyOrder.Issue", "MoneyOrder", moneyOrder.Id.Value.ToString("D"), branchId.Value, "Succeeded", new Dictionary<string, string> { ["Status"] = moneyOrder.Status.ToString(), ["Verified"] = (verifiedIdentityId is not null).ToString() }, cancellationToken);
            return ApplicationResults.Success(ToResponse(moneyOrder, 1));
        }
        catch (DomainException exception) { await access.AuditAsync("MoneyOrder.Issue", "MoneyOrder", branchId.Value.ToString("D"), branchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "domain.rule", ["Verified"] = (verifiedIdentityId is not null).ToString() }, cancellationToken); return DomainFailure<MoneyOrderResponse>(exception); }
    }

    public async Task<ApplicationResult<MoneyOrderResponse>> GetAsync(Guid moneyOrderId, CancellationToken cancellationToken)
    {
        if (!TryId(moneyOrderId, out var id)) return Invalid<MoneyOrderResponse>();
        var moneyOrder = await repository.GetByIdAsync(id, cancellationToken);
        if (moneyOrder is null) return NotFound<MoneyOrderResponse>();
        var authorized = await access.CheckAsync(moneyOrder.Value.OriginatingBranchId.Value, OperationalCapability.Counter, "MoneyOrder.Get", cancellationToken, requireOpenBranch: false);
        return authorized.IsSuccess ? ApplicationResults.Success(ToResponse(moneyOrder.Value, moneyOrder.Version)) : Failure<MoneyOrderResponse>(authorized.Errors);
    }

    public Task<ApplicationResult<MoneyOrderResponse>> MarkPaidAsync(Guid id, long version, CancellationToken token) => ChangeAsync(id, version, static moneyOrder => moneyOrder.MarkPaid(), token);
    public Task<ApplicationResult<MoneyOrderResponse>> CancelAsync(Guid id, long version, CancellationToken token) => ChangeAsync(id, version, static moneyOrder => moneyOrder.Cancel(), token);
    public Task<ApplicationResult<MoneyOrderResponse>> ExpireAsync(Guid id, long version, CancellationToken token) => ChangeAsync(id, version, static moneyOrder => moneyOrder.Expire(), token);

    private async Task<ApplicationResult<MoneyOrderResponse>> ChangeAsync(Guid moneyOrderId, long expectedVersion, Action<MoneyOrder> operation, CancellationToken cancellationToken)
    {
        if (!TryId(moneyOrderId, out var id) || expectedVersion < 1) return Invalid<MoneyOrderResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<MoneyOrderResponse>();
        var authorized = await access.CheckAsync(existing.Value.OriginatingBranchId.Value, OperationalCapability.Counter, "MoneyOrder.Change", cancellationToken);
        if (!authorized.IsSuccess) return Failure<MoneyOrderResponse>(authorized.Errors);
        if (existing.Version != expectedVersion) { await access.AuditAsync("MoneyOrder.Change", "MoneyOrder", existing.Value.Id.Value.ToString("D"), existing.Value.OriginatingBranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict<MoneyOrderResponse>(); }
        try
        {
            operation(existing.Value);
            await access.AuditAsync("MoneyOrder.Change", "MoneyOrder", existing.Value.Id.Value.ToString("D"), existing.Value.OriginatingBranchId.Value, "Started", null, cancellationToken);
            if (!await repository.TryUpdateAsync(existing.Value, expectedVersion, cancellationToken)) { await access.AuditAsync("MoneyOrder.Change", "MoneyOrder", existing.Value.Id.Value.ToString("D"), existing.Value.OriginatingBranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict<MoneyOrderResponse>(); }
            await access.AuditAsync("MoneyOrder.Change", "MoneyOrder", existing.Value.Id.Value.ToString("D"), existing.Value.OriginatingBranchId.Value, "Succeeded", new Dictionary<string, string> { ["Status"] = existing.Value.Status.ToString(), ["Version"] = (expectedVersion + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) }, cancellationToken);
            return ApplicationResults.Success(ToResponse(existing.Value, expectedVersion + 1));
        }
        catch (DomainException exception) { await access.AuditAsync("MoneyOrder.Change", "MoneyOrder", existing.Value.Id.Value.ToString("D"), existing.Value.OriginatingBranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "domain.rule" }, cancellationToken); return DomainFailure<MoneyOrderResponse>(exception); }
    }

    private static MoneyOrderResponse ToResponse(MoneyOrder moneyOrder, long version) => new(moneyOrder.Id.Value, version, moneyOrder.OrderNumber.Value, moneyOrder.Amount.Amount, moneyOrder.Amount.CurrencyCode, moneyOrder.OriginatingBranchId.Value, moneyOrder.Beneficiary.Name, moneyOrder.Beneficiary.Reference, moneyOrder.Status, moneyOrder.IdentityVerificationId?.Value);
    private static bool TryId(Guid value, out EntityId id) { id = value == Guid.Empty ? null! : EntityId.From(value); return value != Guid.Empty; }
    private static ApplicationResult<T> Invalid<T>() => ApplicationResults.Failure<T>([new("request.invalid", "The request is invalid.")]);
    private static ApplicationResult<T> NotFound<T>() => ApplicationResults.Failure<T>([new("resource.not-found", "The requested resource was not found.")]);
    private static ApplicationResult<T> Duplicate<T>(string field) => ApplicationResults.Failure<T>([new("identifier.duplicate", "The business identifier is already in use.", field)]);
    private static ApplicationResult<T> Conflict<T>() => ApplicationResults.Failure<T>([new("concurrency.conflict", "The resource changed before it could be saved. Reload and try again.")]);
    private static ApplicationResult<T> DomainFailure<T>(DomainException exception) => ApplicationResults.Failure<T>([new("domain.rule", exception.Message)]);
    private static ApplicationResult<T> Failure<T>(IReadOnlyList<ValidationError> errors) => ApplicationResults.Failure<T>(errors);
}

public sealed record MoneyOrderPolicy
{
    public MoneyOrderPolicy(decimal identityVerificationThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(identityVerificationThreshold);
        IdentityVerificationThreshold = identityVerificationThreshold;
    }

    public decimal IdentityVerificationThreshold { get; }
}
