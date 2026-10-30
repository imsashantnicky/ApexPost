using ApexPost.Application.Results;
using ApexPost.Domain.Common;
using ApexPost.Domain.Payments;
using ApexPost.Domain.Transactions;
using ApexPost.Application.Security;

namespace ApexPost.Application.Transactions;

public sealed class TransactionOperationsUseCase
{
    private readonly IRetailTransactionRepository repository;
    private readonly IServicePriceLookup prices;
    private readonly ITransactionPaymentCoordinator paymentCoordinator;
    private readonly OperationalAccess access;

    public TransactionOperationsUseCase(IRetailTransactionRepository repository, IServicePriceLookup prices, ITransactionPaymentCoordinator paymentCoordinator, OperationalAccess access)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.prices = prices ?? throw new ArgumentNullException(nameof(prices));
        this.paymentCoordinator = paymentCoordinator ?? throw new ArgumentNullException(nameof(paymentCoordinator));
        this.access = access;
    }

    public async Task<ApplicationResult<TransactionResponse>> CreateAsync(CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.BranchId, out var branchId)) return Invalid<TransactionResponse>();
        var authorized = await access.CheckAsync(branchId.Value, OperationalCapability.Counter, "Transaction.Create", cancellationToken);
        if (!authorized.IsSuccess) return Failure<TransactionResponse>(authorized.Errors);
        try
        {
            var transaction = new RetailTransaction(EntityId.New(), branchId, request.CounterReference, request.CurrencyCode, DateTimeOffset.UtcNow);
            await access.AuditAsync("Transaction.Create", "RetailTransaction", transaction.Id.Value.ToString("D"), branchId.Value, "Started", null, cancellationToken);
            if (!await repository.TryAddAsync(transaction, cancellationToken)) { await access.AuditAsync("Transaction.Create", "RetailTransaction", transaction.Id.Value.ToString("D"), branchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict<TransactionResponse>(); }
            await access.AuditAsync("Transaction.Create", "RetailTransaction", transaction.Id.Value.ToString("D"), branchId.Value, "Succeeded", null, cancellationToken);
            return ApplicationResults.Success(ToResponse(transaction, 1));
        }
        catch (DomainException exception) { await access.AuditAsync("Transaction.Create", "RetailTransaction", branchId.Value.ToString("D"), branchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "domain.rule" }, cancellationToken); return DomainFailure<TransactionResponse>(exception); }
    }

    public async Task<ApplicationResult<TransactionResponse>> GetAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        if (!TryId(transactionId, out var id)) return Invalid<TransactionResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<TransactionResponse>();
        var authorized = await access.CheckAsync(existing.Value.BranchId.Value, OperationalCapability.Counter, "Transaction.Get", cancellationToken, requireOpenBranch: false);
        return authorized.IsSuccess ? ApplicationResults.Success(ToResponse(existing.Value, existing.Version)) : Failure<TransactionResponse>(authorized.Errors);
    }

    public async Task<ApplicationResult<IReadOnlyList<TransactionResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        var scope = await access.AuthorizeListAsync(OperationalCapability.Counter, "Transaction.List", cancellationToken);
        if (!scope.IsSuccess) return ApplicationResults.Failure<IReadOnlyList<TransactionResponse>>(scope.Errors);
        var transactions = await repository.ListAsync(cancellationToken);
        return ApplicationResults.Success<IReadOnlyList<TransactionResponse>>(transactions
            .Where(item => scope.Value.Includes(item.Value.BranchId.Value))
            .Select(item => ToResponse(item.Value, item.Version))
            .OrderBy(item => item.CounterReference, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    public async Task<ApplicationResult<TransactionResponse>> AddLineAsync(AddTransactionLineRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.TransactionId, out var id) || !TryId(request.ServiceId, out var serviceId) || request.ExpectedVersion < 1) return Invalid<TransactionResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<TransactionResponse>();
        var authorized = await access.CheckAsync(existing.Value.BranchId.Value, OperationalCapability.Counter, "Transaction.AddLine", cancellationToken);
        if (!authorized.IsSuccess) return Failure<TransactionResponse>(authorized.Errors);
        if (existing.Version != request.ExpectedVersion) { await access.AuditAsync("Transaction.AddLine", "RetailTransaction", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict<TransactionResponse>(); }
        var price = await prices.GetCurrentPriceAsync(serviceId, existing.Value.CurrencyCode, cancellationToken);
        if (price is null) return ApplicationResults.Failure<TransactionResponse>([new("service.price.unavailable", "No current price is available for the selected service.", "serviceId")]);
        try
        {
            existing.Value.AddLineItem(serviceId, request.Description, request.Quantity, price.Amount);
            await access.AuditAsync("Transaction.AddLine", "RetailTransaction", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Started", null, cancellationToken);
            if (!await repository.TryUpdateAsync(existing.Value, request.ExpectedVersion, cancellationToken)) { await access.AuditAsync("Transaction.AddLine", "RetailTransaction", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict<TransactionResponse>(); }
            await access.AuditAsync("Transaction.AddLine", "RetailTransaction", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Succeeded", new Dictionary<string, string> { ["Version"] = (request.ExpectedVersion + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) }, cancellationToken);
            return ApplicationResults.Success(ToResponse(existing.Value, request.ExpectedVersion + 1));
        }
        catch (DomainException exception) { await access.AuditAsync("Transaction.AddLine", "RetailTransaction", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "domain.rule" }, cancellationToken); return DomainFailure<TransactionResponse>(exception); }
    }

    public async Task<ApplicationResult<TransactionResponse>> StartPaymentAsync(StartPaymentRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.TransactionId, out var id) || request.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(request.IdempotencyKey)) return Invalid<TransactionResponse>();
        var duplicate = await paymentCoordinator.GetByIdempotencyKeyAsync(request.IdempotencyKey.Trim(), cancellationToken);
        if (duplicate is not null)
        {
            var duplicateAccess = await access.CheckAsync(duplicate.Value.BranchId.Value, OperationalCapability.Counter, "Transaction.RetryPayment", cancellationToken);
            if (!duplicateAccess.IsSuccess) return Failure<TransactionResponse>(duplicateAccess.Errors);
            return duplicate.Value.Id == id
                ? ApplicationResults.Success(ToResponse(duplicate.Value, duplicate.Version))
                : ApplicationResults.Failure<TransactionResponse>([new("idempotency.conflict", "The idempotency key is already associated with another transaction.", "idempotencyKey")]);
        }
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<TransactionResponse>();
        var authorized = await access.CheckAsync(existing.Value.BranchId.Value, OperationalCapability.Counter, "Transaction.StartPayment", cancellationToken);
        if (!authorized.IsSuccess) return Failure<TransactionResponse>(authorized.Errors);
        if (existing.Version != request.ExpectedVersion) { await access.AuditAsync("Transaction.StartPayment", "RetailTransaction", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict<TransactionResponse>(); }
        try
        {
            existing.Value.StartPayment(request.Amount, request.Method, request.Reference);
            await access.AuditAsync("Transaction.StartPayment", "RetailTransaction", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Started", null, cancellationToken);
            var write = await paymentCoordinator.TryStartPaymentAsync(existing.Value, request.ExpectedVersion, request.IdempotencyKey.Trim(), cancellationToken);
            if (write.IsDuplicate)
            {
                var duplicateTransaction = write.Transaction ?? await paymentCoordinator.GetByIdempotencyKeyAsync(request.IdempotencyKey.Trim(), cancellationToken);
                if (duplicateTransaction is null) return Conflict<TransactionResponse>();
                var duplicateAccess = await access.CheckAsync(duplicateTransaction.Value.BranchId.Value, OperationalCapability.Counter, "Transaction.RetryPayment", cancellationToken);
                if (!duplicateAccess.IsSuccess) return Failure<TransactionResponse>(duplicateAccess.Errors);
                return duplicateTransaction.Value.Id == id
                    ? ApplicationResults.Success(ToResponse(duplicateTransaction.Value, duplicateTransaction.Version))
                    : ApplicationResults.Failure<TransactionResponse>([new("idempotency.conflict", "The idempotency key is already associated with another transaction.", "idempotencyKey")]);
            }
            if (write.Transaction is null) { await access.AuditAsync("Transaction.StartPayment", "RetailTransaction", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict<TransactionResponse>(); }
            await access.AuditAsync("Transaction.StartPayment", "RetailTransaction", write.Transaction.Value.Id.Value.ToString("D"), write.Transaction.Value.BranchId.Value, "Succeeded", new Dictionary<string, string> { ["Status"] = write.Transaction.Value.Status.ToString(), ["Version"] = write.Transaction.Version.ToString(System.Globalization.CultureInfo.InvariantCulture) }, cancellationToken);
            return ApplicationResults.Success(ToResponse(write.Transaction.Value, write.Transaction.Version));
        }
        catch (DomainException exception) { await access.AuditAsync("Transaction.StartPayment", "RetailTransaction", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "domain.rule" }, cancellationToken); return DomainFailure<TransactionResponse>(exception); }
    }

    public Task<ApplicationResult<TransactionResponse>> ConfirmPaymentAsync(Guid id, long version, CancellationToken token) => ChangeAsync(id, version, static transaction => transaction.ConfirmPayment(), OperationalCapability.Counter, "Transaction.PaymentConfirmed", token);
    public Task<ApplicationResult<TransactionResponse>> DeclinePaymentAsync(Guid id, long version, string reason, CancellationToken token) => ChangeAsync(id, version, transaction => transaction.DeclinePayment(reason), OperationalCapability.Counter, "Transaction.PaymentDeclined", token);
    public Task<ApplicationResult<TransactionResponse>> CancelAsync(Guid id, long version, string reason, CancellationToken token) => ChangeAsync(id, version, transaction => transaction.Cancel(reason), OperationalCapability.BranchManager, "Transaction.Void", token);
    public Task<ApplicationResult<TransactionResponse>> ReversePaymentAsync(Guid id, long version, string reason, CancellationToken token) => ChangeAsync(id, version, transaction => transaction.ReversePayment(reason), OperationalCapability.BranchManager, "Transaction.Refund", token);

    public async Task<ApplicationResult<ReceiptResponse>> GetReceiptAsync(Guid transactionId, CancellationToken cancellationToken)
    {
        if (!TryId(transactionId, out var id)) return Invalid<ReceiptResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<ReceiptResponse>();
        var authorized = await access.CheckAsync(existing.Value.BranchId.Value, OperationalCapability.Counter, "Transaction.Receipt", cancellationToken, requireOpenBranch: false);
        if (!authorized.IsSuccess) return Failure<ReceiptResponse>(authorized.Errors);
        if (existing.Value.Status != TransactionStatus.Paid) return ApplicationResults.Failure<ReceiptResponse>([new("transaction.not-paid", "A receipt is available only for a paid transaction.")]);
        var payment = existing.Value.Payments.Single(record => record.Status == PaymentStatus.Confirmed);
        return ApplicationResults.Success(new ReceiptResponse($"R-{existing.Value.Id.Value:N}", existing.Value.Id.Value, existing.Value.CounterReference, existing.Value.Total, existing.Value.CurrencyCode, existing.Value.LineItems.Select(ToLine).ToArray(), ToPayment(payment)));
    }

    private async Task<ApplicationResult<TransactionResponse>> ChangeAsync(Guid transactionId, long expectedVersion, Action<RetailTransaction> operation, OperationalCapability capability, string operationName, CancellationToken cancellationToken)
    {
        if (!TryId(transactionId, out var id) || expectedVersion < 1) return Invalid<TransactionResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<TransactionResponse>();
        var authorized = await access.CheckAsync(existing.Value.BranchId.Value, capability, operationName, cancellationToken);
        if (!authorized.IsSuccess) return Failure<TransactionResponse>(authorized.Errors);
        if (existing.Version != expectedVersion) { await access.AuditAsync(operationName, "RetailTransaction", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict<TransactionResponse>(); }
        try
        {
            operation(existing.Value);
            await access.AuditAsync(operationName, "RetailTransaction", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Started", null, cancellationToken);
            if (!await repository.TryUpdateAsync(existing.Value, expectedVersion, cancellationToken)) { await access.AuditAsync(operationName, "RetailTransaction", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict<TransactionResponse>(); }
            await access.AuditAsync(operationName, "RetailTransaction", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Succeeded", new Dictionary<string, string> { ["Status"] = existing.Value.Status.ToString(), ["Version"] = (expectedVersion + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) }, cancellationToken);
            return ApplicationResults.Success(ToResponse(existing.Value, expectedVersion + 1));
        }
        catch (DomainException exception) { await access.AuditAsync(operationName, "RetailTransaction", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "domain.rule" }, cancellationToken); return DomainFailure<TransactionResponse>(exception); }
    }

    private static TransactionResponse ToResponse(RetailTransaction transaction, long version) => new(transaction.Id.Value, version, transaction.BranchId.Value, transaction.CounterReference, transaction.CurrencyCode, transaction.Total, transaction.Status, transaction.LineItems.Select(ToLine).ToArray(), transaction.Payments.Select(ToPayment).ToArray());
    private static TransactionLineResponse ToLine(TransactionLineItem line) => new(line.Id.Value, line.ServiceId.Value, line.Description, line.Quantity, line.UnitPrice, line.Total);
    private static PaymentResponse ToPayment(PaymentRecord payment) => new(payment.Id.Value, payment.Amount, payment.CurrencyCode, payment.Method, payment.Reference, payment.Status);
    private static bool TryId(Guid value, out EntityId id) { id = value == Guid.Empty ? null! : EntityId.From(value); return value != Guid.Empty; }
    private static ApplicationResult<T> Invalid<T>() => ApplicationResults.Failure<T>([new("request.invalid", "The request is invalid.")]);
    private static ApplicationResult<T> NotFound<T>() => ApplicationResults.Failure<T>([new("resource.not-found", "The requested resource was not found.")]);
    private static ApplicationResult<T> Conflict<T>() => ApplicationResults.Failure<T>([new("concurrency.conflict", "The resource changed before it could be saved. Reload and try again.")]);
    private static ApplicationResult<T> DomainFailure<T>(DomainException exception) => ApplicationResults.Failure<T>([new("domain.rule", exception.Message)]);
    private static ApplicationResult<T> Failure<T>(IReadOnlyList<ValidationError> errors) => ApplicationResults.Failure<T>(errors);
}
