using ApexPost.Application.Results;
using ApexPost.Domain.Common;
using ApexPost.Domain.Payments;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Transactions;

public sealed class TransactionOperationsUseCase
{
    private readonly IRetailTransactionRepository repository;
    private readonly IServicePriceLookup prices;
    private readonly ITransactionPaymentCoordinator paymentCoordinator;

    public TransactionOperationsUseCase(IRetailTransactionRepository repository, IServicePriceLookup prices, ITransactionPaymentCoordinator paymentCoordinator)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.prices = prices ?? throw new ArgumentNullException(nameof(prices));
        this.paymentCoordinator = paymentCoordinator ?? throw new ArgumentNullException(nameof(paymentCoordinator));
    }

    public async Task<ApplicationResult<TransactionResponse>> CreateAsync(CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.BranchId, out var branchId)) return Invalid<TransactionResponse>();
        try
        {
            var transaction = new RetailTransaction(EntityId.New(), branchId, request.CounterReference, request.CurrencyCode);
            if (!await repository.TryAddAsync(transaction, cancellationToken)) return Conflict<TransactionResponse>();
            return ApplicationResults.Success(ToResponse(transaction, 1));
        }
        catch (DomainException exception) { return DomainFailure<TransactionResponse>(exception); }
    }

    public async Task<ApplicationResult<TransactionResponse>> GetAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        if (!TryId(transactionId, out var id)) return Invalid<TransactionResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        return existing is null ? NotFound<TransactionResponse>() : ApplicationResults.Success(ToResponse(existing.Value, existing.Version));
    }

    public async Task<ApplicationResult<TransactionResponse>> AddLineAsync(AddTransactionLineRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.TransactionId, out var id) || !TryId(request.ServiceId, out var serviceId) || request.ExpectedVersion < 1) return Invalid<TransactionResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<TransactionResponse>();
        if (existing.Version != request.ExpectedVersion) return Conflict<TransactionResponse>();
        var price = await prices.GetCurrentPriceAsync(serviceId, existing.Value.CurrencyCode, cancellationToken);
        if (price is null) return ApplicationResults.Failure<TransactionResponse>([new("service.price.unavailable", "No current price is available for the selected service.", "serviceId")]);
        try
        {
            existing.Value.AddLineItem(serviceId, request.Description, request.Quantity, price.Amount);
            return await repository.TryUpdateAsync(existing.Value, request.ExpectedVersion, cancellationToken)
                ? ApplicationResults.Success(ToResponse(existing.Value, request.ExpectedVersion + 1)) : Conflict<TransactionResponse>();
        }
        catch (DomainException exception) { return DomainFailure<TransactionResponse>(exception); }
    }

    public async Task<ApplicationResult<TransactionResponse>> StartPaymentAsync(StartPaymentRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.TransactionId, out var id) || request.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(request.IdempotencyKey)) return Invalid<TransactionResponse>();
        var duplicate = await paymentCoordinator.GetByIdempotencyKeyAsync(request.IdempotencyKey.Trim(), cancellationToken);
        if (duplicate is not null)
        {
            return duplicate.Value.Id == id
                ? ApplicationResults.Success(ToResponse(duplicate.Value, duplicate.Version))
                : ApplicationResults.Failure<TransactionResponse>([new("idempotency.conflict", "The idempotency key is already associated with another transaction.", "idempotencyKey")]);
        }
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<TransactionResponse>();
        if (existing.Version != request.ExpectedVersion) return Conflict<TransactionResponse>();
        try
        {
            existing.Value.StartPayment(request.Amount, request.Method, request.Reference);
            var write = await paymentCoordinator.TryStartPaymentAsync(existing.Value, request.ExpectedVersion, request.IdempotencyKey.Trim(), cancellationToken);
            if (write.Transaction is null) return Conflict<TransactionResponse>();
            return ApplicationResults.Success(ToResponse(write.Transaction.Value, write.Transaction.Version));
        }
        catch (DomainException exception) { return DomainFailure<TransactionResponse>(exception); }
    }

    public Task<ApplicationResult<TransactionResponse>> ConfirmPaymentAsync(Guid id, long version, CancellationToken token) => ChangeAsync(id, version, static transaction => transaction.ConfirmPayment(), token);
    public Task<ApplicationResult<TransactionResponse>> DeclinePaymentAsync(Guid id, long version, string reason, CancellationToken token) => ChangeAsync(id, version, transaction => transaction.DeclinePayment(reason), token);
    public Task<ApplicationResult<TransactionResponse>> CancelAsync(Guid id, long version, CancellationToken token) => ChangeAsync(id, version, static transaction => transaction.Cancel(), token);
    public Task<ApplicationResult<TransactionResponse>> ReversePaymentAsync(Guid id, long version, CancellationToken token) => ChangeAsync(id, version, static transaction => transaction.ReversePayment(), token);

    public async Task<ApplicationResult<ReceiptResponse>> GetReceiptAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        if (!TryId(transactionId, out var id)) return Invalid<ReceiptResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<ReceiptResponse>();
        if (existing.Value.Status != TransactionStatus.Paid) return ApplicationResults.Failure<ReceiptResponse>([new("transaction.not-paid", "A receipt is available only for a paid transaction.")]);
        var payment = existing.Value.Payments.Single(record => record.Status == PaymentStatus.Confirmed);
        return ApplicationResults.Success(new ReceiptResponse($"R-{existing.Value.Id.Value:N}", existing.Value.Id.Value, existing.Value.CounterReference, existing.Value.Total, existing.Value.CurrencyCode, existing.Value.LineItems.Select(ToLine).ToArray(), ToPayment(payment)));
    }

    private async Task<ApplicationResult<TransactionResponse>> ChangeAsync(Guid transactionId, long expectedVersion, Action<RetailTransaction> operation, CancellationToken cancellationToken)
    {
        if (!TryId(transactionId, out var id) || expectedVersion < 1) return Invalid<TransactionResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<TransactionResponse>();
        if (existing.Version != expectedVersion) return Conflict<TransactionResponse>();
        try
        {
            operation(existing.Value);
            return await repository.TryUpdateAsync(existing.Value, expectedVersion, cancellationToken)
                ? ApplicationResults.Success(ToResponse(existing.Value, expectedVersion + 1)) : Conflict<TransactionResponse>();
        }
        catch (DomainException exception) { return DomainFailure<TransactionResponse>(exception); }
    }

    private static TransactionResponse ToResponse(RetailTransaction transaction, long version) => new(transaction.Id.Value, version, transaction.BranchId.Value, transaction.CounterReference, transaction.CurrencyCode, transaction.Total, transaction.Status, transaction.LineItems.Select(ToLine).ToArray(), transaction.Payments.Select(ToPayment).ToArray());
    private static TransactionLineResponse ToLine(TransactionLineItem line) => new(line.Id.Value, line.ServiceId.Value, line.Description, line.Quantity, line.UnitPrice, line.Total);
    private static PaymentResponse ToPayment(PaymentRecord payment) => new(payment.Id.Value, payment.Amount, payment.CurrencyCode, payment.Method, payment.Reference, payment.Status);
    private static bool TryId(Guid value, out EntityId id) { id = value == Guid.Empty ? null! : EntityId.From(value); return value != Guid.Empty; }
    private static ApplicationResult<T> Invalid<T>() => ApplicationResults.Failure<T>([new("request.invalid", "The request is invalid.")]);
    private static ApplicationResult<T> NotFound<T>() => ApplicationResults.Failure<T>([new("resource.not-found", "The requested resource was not found.")]);
    private static ApplicationResult<T> Conflict<T>() => ApplicationResults.Failure<T>([new("concurrency.conflict", "The resource changed before it could be saved. Reload and try again.")]);
    private static ApplicationResult<T> DomainFailure<T>(DomainException exception) => ApplicationResults.Failure<T>([new("domain.rule", exception.Message)]);
}
