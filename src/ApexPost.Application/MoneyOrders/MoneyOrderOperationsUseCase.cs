using ApexPost.Application.Results;
using ApexPost.Domain.Common;
using ApexPost.Domain.MoneyOrders;

namespace ApexPost.Application.MoneyOrders;

public sealed class MoneyOrderOperationsUseCase
{
    private readonly IMoneyOrderRepository repository;
    public MoneyOrderOperationsUseCase(IMoneyOrderRepository repository) => this.repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<ApplicationResult<MoneyOrderResponse>> IssueAsync(IssueMoneyOrderRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.OriginatingBranchId, out var branchId)) return Invalid<MoneyOrderResponse>();
        try
        {
            var moneyOrder = new MoneyOrder(EntityId.New(), MoneyOrderNumber.From(request.OrderNumber), new MoneyOrderAmount(request.Amount, request.CurrencyCode), branchId, new MoneyOrderBeneficiary(request.BeneficiaryName, request.BeneficiaryReference));
            if (!await repository.TryAddAsync(moneyOrder, cancellationToken)) return Duplicate<MoneyOrderResponse>("orderNumber");
            return ApplicationResults.Success(ToResponse(moneyOrder, 1));
        }
        catch (DomainException exception) { return DomainFailure<MoneyOrderResponse>(exception); }
    }

    public async Task<ApplicationResult<MoneyOrderResponse>> GetAsync(Guid moneyOrderId, CancellationToken cancellationToken)
    {
        if (!TryId(moneyOrderId, out var id)) return Invalid<MoneyOrderResponse>();
        var moneyOrder = await repository.GetByIdAsync(id, cancellationToken);
        return moneyOrder is null ? NotFound<MoneyOrderResponse>() : ApplicationResults.Success(ToResponse(moneyOrder.Value, moneyOrder.Version));
    }

    public Task<ApplicationResult<MoneyOrderResponse>> MarkPaidAsync(Guid id, long version, CancellationToken token) => ChangeAsync(id, version, static moneyOrder => moneyOrder.MarkPaid(), token);
    public Task<ApplicationResult<MoneyOrderResponse>> CancelAsync(Guid id, long version, CancellationToken token) => ChangeAsync(id, version, static moneyOrder => moneyOrder.Cancel(), token);
    public Task<ApplicationResult<MoneyOrderResponse>> ExpireAsync(Guid id, long version, CancellationToken token) => ChangeAsync(id, version, static moneyOrder => moneyOrder.Expire(), token);

    private async Task<ApplicationResult<MoneyOrderResponse>> ChangeAsync(Guid moneyOrderId, long expectedVersion, Action<MoneyOrder> operation, CancellationToken cancellationToken)
    {
        if (!TryId(moneyOrderId, out var id) || expectedVersion < 1) return Invalid<MoneyOrderResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<MoneyOrderResponse>();
        if (existing.Version != expectedVersion) return Conflict<MoneyOrderResponse>();
        try
        {
            operation(existing.Value);
            return await repository.TryUpdateAsync(existing.Value, expectedVersion, cancellationToken)
                ? ApplicationResults.Success(ToResponse(existing.Value, expectedVersion + 1)) : Conflict<MoneyOrderResponse>();
        }
        catch (DomainException exception) { return DomainFailure<MoneyOrderResponse>(exception); }
    }

    private static MoneyOrderResponse ToResponse(MoneyOrder moneyOrder, long version) => new(moneyOrder.Id.Value, version, moneyOrder.OrderNumber.Value, moneyOrder.Amount.Amount, moneyOrder.Amount.CurrencyCode, moneyOrder.OriginatingBranchId.Value, moneyOrder.Beneficiary.Name, moneyOrder.Beneficiary.Reference, moneyOrder.Status);
    private static bool TryId(Guid value, out EntityId id) { id = value == Guid.Empty ? null! : EntityId.From(value); return value != Guid.Empty; }
    private static ApplicationResult<T> Invalid<T>() => ApplicationResults.Failure<T>([new("request.invalid", "The request is invalid.")]);
    private static ApplicationResult<T> NotFound<T>() => ApplicationResults.Failure<T>([new("resource.not-found", "The requested resource was not found.")]);
    private static ApplicationResult<T> Duplicate<T>(string field) => ApplicationResults.Failure<T>([new("identifier.duplicate", "The business identifier is already in use.", field)]);
    private static ApplicationResult<T> Conflict<T>() => ApplicationResults.Failure<T>([new("concurrency.conflict", "The resource changed before it could be saved. Reload and try again.")]);
    private static ApplicationResult<T> DomainFailure<T>(DomainException exception) => ApplicationResults.Failure<T>([new("domain.rule", exception.Message)]);
}
