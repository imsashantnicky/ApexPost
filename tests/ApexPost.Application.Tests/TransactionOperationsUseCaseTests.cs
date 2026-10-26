using ApexPost.Application.Persistence;
using ApexPost.Application.Transactions;
using ApexPost.Domain.Common;
using ApexPost.Domain.Payments;
using ApexPost.Domain.Pricing;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Tests;

public class TransactionOperationsUseCaseTests
{
    [Fact]
    public async Task TransactionUsesAuthoritativePriceAndCalculatesDeterministicTotal()
    {
        var fixture = new Fixture(ServicePrice.From(12.50m, "INR"));
        var created = await fixture.UseCase.CreateAsync(new(Guid.NewGuid(), "Counter 1", "INR"), default);

        var updated = await fixture.UseCase.AddLineAsync(new(created.Value.Id, 1, Guid.NewGuid(), "Express parcel", 2), default);

        Assert.Equal(25.00m, updated.Value.Total);
        Assert.Equal(12.50m, Assert.Single(updated.Value.Lines).UnitPrice);
    }

    [Fact]
    public async Task MissingPriceFailsWithoutAddingAPartialLineAndCanBeRetried()
    {
        var fixture = new Fixture(null);
        var created = await fixture.UseCase.CreateAsync(new(Guid.NewGuid(), "Counter 1", "INR"), default);
        var request = new AddTransactionLineRequest(created.Value.Id, 1, Guid.NewGuid(), "Express parcel", 1);

        var failed = await fixture.UseCase.AddLineAsync(request, default);
        fixture.PriceLookup.Price = ServicePrice.From(10m, "INR");
        var recovered = await fixture.UseCase.AddLineAsync(request, default);

        Assert.Equal("service.price.unavailable", Assert.Single(failed.Errors).Code);
        Assert.Equal(10m, recovered.Value.Total);
        Assert.Single(recovered.Value.Lines);
    }

    [Fact]
    public async Task PaymentStartIsIdempotentAndDoesNotCreateADuplicatePayment()
    {
        var fixture = new Fixture(ServicePrice.From(10m, "INR"));
        var transaction = await fixture.CreateWithLineAsync();
        var request = new StartPaymentRequest(transaction.Id, transaction.Version, 10m, PaymentMethod.Cash, "RCPT-1", "payment-attempt-001");

        var first = await fixture.UseCase.StartPaymentAsync(request, default);
        var retry = await fixture.UseCase.StartPaymentAsync(request, default);

        Assert.Equal(TransactionStatus.PaymentPending, first.Value.Status);
        Assert.Equal(first.Value.Version, retry.Value.Version);
        Assert.Single(retry.Value.Payments);
    }

    [Fact]
    public async Task IdempotencyKeyCannotBeReusedForAnotherTransaction()
    {
        var fixture = new Fixture(ServicePrice.From(10m, "INR"));
        var first = await fixture.CreateWithLineAsync();
        var second = await fixture.CreateWithLineAsync();
        await fixture.UseCase.StartPaymentAsync(new(first.Id, first.Version, 10m, PaymentMethod.Cash, "RCPT-1", "payment-attempt-001"), default);

        var conflict = await fixture.UseCase.StartPaymentAsync(new(second.Id, second.Version, 10m, PaymentMethod.Cash, "RCPT-2", "payment-attempt-001"), default);

        Assert.Equal("idempotency.conflict", Assert.Single(conflict.Errors).Code);
    }

    [Fact]
    public async Task PaymentFailureLeavesTransactionOpenWithoutPaymentRecord()
    {
        var fixture = new Fixture(ServicePrice.From(10m, "INR"));
        var transaction = await fixture.CreateWithLineAsync();

        var failed = await fixture.UseCase.StartPaymentAsync(new(transaction.Id, transaction.Version, 9m, PaymentMethod.Cash, "RCPT-1", "payment-attempt-001"), default);
        var current = await fixture.UseCase.GetAsync(transaction.Id, default);

        Assert.Equal("domain.rule", Assert.Single(failed.Errors).Code);
        Assert.Equal(TransactionStatus.Open, current.Value.Status);
        Assert.Empty(current.Value.Payments);
    }

    [Fact]
    public async Task ConfirmedPaymentProducesReceiptAndCanBeReversed()
    {
        var fixture = new Fixture(ServicePrice.From(10m, "INR"));
        var transaction = await fixture.CreateWithLineAsync();
        var pending = await fixture.UseCase.StartPaymentAsync(new(transaction.Id, transaction.Version, 10m, PaymentMethod.Cash, "RCPT-1", "payment-attempt-001"), default);
        var paid = await fixture.UseCase.ConfirmPaymentAsync(transaction.Id, pending.Value.Version, default);
        var receipt = await fixture.UseCase.GetReceiptAsync(transaction.Id, default);
        var reversed = await fixture.UseCase.ReversePaymentAsync(transaction.Id, paid.Value.Version, "Customer refund", default);

        Assert.Equal(TransactionStatus.Paid, paid.Value.Status);
        Assert.StartsWith("R-", receipt.Value.ReceiptNumber, StringComparison.Ordinal);
        Assert.Equal(10m, receipt.Value.Total);
        Assert.Equal(TransactionStatus.Reversed, reversed.Value.Status);
    }

    [Fact]
    public async Task PendingTransactionCanBeCancelledAndDoesNotHaveReceipt()
    {
        var fixture = new Fixture(ServicePrice.From(10m, "INR"));
        var transaction = await fixture.CreateWithLineAsync();
        var pending = await fixture.UseCase.StartPaymentAsync(new(transaction.Id, transaction.Version, 10m, PaymentMethod.Card, "CARD-1", "payment-attempt-001"), default);

        var cancelled = await fixture.UseCase.CancelAsync(transaction.Id, pending.Value.Version, "Customer withdrew", default);
        var receipt = await fixture.UseCase.GetReceiptAsync(transaction.Id, default);

        Assert.Equal(TransactionStatus.Cancelled, cancelled.Value.Status);
        Assert.Equal("transaction.not-paid", Assert.Single(receipt.Errors).Code);
    }

    [Fact]
    public async Task CounterClerkCannotVoidTransaction()
    {
        var fixture = new Fixture(ServicePrice.From(10m, "INR"));
        var transaction = await fixture.CreateWithLineAsync();
        fixture.Actor.ChangeRole("CounterStaff");
        var result = await fixture.UseCase.CancelAsync(transaction.Id, transaction.Version, "Customer withdrew", default);
        Assert.Equal("access.denied", Assert.Single(result.Errors).Code);
    }

    private sealed class Fixture
    {
        public MutableActor Actor { get; } = new(Guid.NewGuid(), "Administrator");

        public Fixture(ServicePrice? price)
        {
            Repository = new TransactionRepositoryDouble();
            PriceLookup = new PriceLookupDouble(price);
            Coordinator = new PaymentCoordinatorDouble(Repository);
            UseCase = new TransactionOperationsUseCase(Repository, PriceLookup, Coordinator, OperationalAccessFixture.Create(branchId: Actor.AssignedBranchId, currentActor: Actor));
        }

        public TransactionRepositoryDouble Repository { get; }
        public PriceLookupDouble PriceLookup { get; }
        public PaymentCoordinatorDouble Coordinator { get; }
        public TransactionOperationsUseCase UseCase { get; }

        public async Task<TransactionResponse> CreateWithLineAsync()
        {
            var created = await UseCase.CreateAsync(new(Actor.AssignedBranchId, "Counter 1", "INR"), default);
            var withLine = await UseCase.AddLineAsync(new(created.Value.Id, created.Value.Version, Guid.NewGuid(), "Express parcel", 1), default);
            return withLine.Value;
        }
    }

    private sealed class MutableActor(Guid assignedBranchId, string role) : ApexPost.Application.Security.ICurrentActor
    {
        public ApexPost.Application.Security.ActorContext? Actor { get; private set; } = new(Guid.NewGuid(), role, assignedBranchId, "test-correlation");
        public Guid AssignedBranchId { get; } = assignedBranchId;
        public void ChangeRole(string newRole) => Actor = Actor! with { Role = newRole };
    }

    private sealed class PriceLookupDouble(ServicePrice? price) : IServicePriceLookup
    {
        public ServicePrice? Price { get; set; } = price;
        public Task<ServicePrice?> GetCurrentPriceAsync(EntityId serviceId, string currencyCode, CancellationToken cancellationToken) => Task.FromResult(Price);
    }

    private sealed class TransactionRepositoryDouble : IRetailTransactionRepository
    {
        private readonly Dictionary<Guid, Versioned<RetailTransaction>> items = [];
        public Task<IReadOnlyList<Versioned<RetailTransaction>>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Versioned<RetailTransaction>>>(items.Values.ToArray());
        public Task<Versioned<RetailTransaction>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken) => Task.FromResult(items.GetValueOrDefault(id.Value));
        public Task<bool> TryAddAsync(RetailTransaction transaction, CancellationToken cancellationToken) { items[transaction.Id.Value] = new(transaction, 1); return Task.FromResult(true); }
        public Task<bool> TryUpdateAsync(RetailTransaction transaction, long expectedVersion, CancellationToken cancellationToken) => TryStoreAsync(transaction, expectedVersion);
        public Task<bool> TryStoreAsync(RetailTransaction transaction, long expectedVersion)
        {
            if (!items.TryGetValue(transaction.Id.Value, out var existing) || existing.Version != expectedVersion) return Task.FromResult(false);
            items[transaction.Id.Value] = new(transaction, expectedVersion + 1); return Task.FromResult(true);
        }
    }

    private sealed class PaymentCoordinatorDouble(TransactionRepositoryDouble repository) : ITransactionPaymentCoordinator
    {
        private readonly Dictionary<string, Guid> keys = new(StringComparer.Ordinal);
        public async Task<Versioned<RetailTransaction>?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
        {
            return keys.TryGetValue(idempotencyKey, out var transactionId)
                ? await repository.GetByIdAsync(EntityId.From(transactionId), cancellationToken)
                : null;
        }
        public async Task<PaymentStartWriteResult> TryStartPaymentAsync(RetailTransaction transaction, long expectedVersion, string idempotencyKey, CancellationToken cancellationToken)
        {
            if (keys.ContainsKey(idempotencyKey)) return new(await GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken), true);
            if (!await repository.TryStoreAsync(transaction, expectedVersion)) return new(null, false);
            keys.Add(idempotencyKey, transaction.Id.Value);
            return new(await repository.GetByIdAsync(transaction.Id, cancellationToken), false);
        }
    }
}
