using ApexPost.Application.Transactions;
using ApexPost.Domain.Common;
using ApexPost.Domain.Payments;
using ApexPost.Domain.Transactions;
using ApexPost.Infrastructure.Persistence.Json;

namespace ApexPost.Infrastructure.Tests;

public sealed class JsonRetailTransactionRepositoryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ApexPostTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CancelledAndReversedTransactionsRestoreTheirReasons()
    {
        var repository = new JsonRetailTransactionRepository(new JsonDataOptions { DataDirectory = directory });
        var cancelled = CreateTransaction("C-100", "Void reason");
        Assert.True(await repository.TryAddAsync(cancelled, default));
        var loadedCancelled = await repository.GetByIdAsync(cancelled.Id, default);

        var reversed = CreateTransaction("C-101", "Refund reason", reverse: true);
        Assert.True(await repository.TryAddAsync(reversed, default));
        var loadedReversed = await repository.GetByIdAsync(reversed.Id, default);

        Assert.Equal(TransactionStatus.Cancelled, loadedCancelled!.Value.Status);
        Assert.Equal("Void reason", loadedCancelled.Value.VoidReason);
        Assert.Equal(TransactionStatus.Reversed, loadedReversed!.Value.Status);
        Assert.Equal("Refund reason", Assert.Single(loadedReversed.Value.Payments).ReversalReason);
    }

    [Fact]
    public async Task Reload_AfterDeclineThenAddLine_Succeeds()
    {
        var repository = new JsonRetailTransactionRepository(new JsonDataOptions { DataDirectory = directory });
        var transaction = new RetailTransaction(EntityId.New(), EntityId.New(), "C-200", "INR");
        transaction.AddLineItem(EntityId.New(), "Counter service", 1, 10m);
        Assert.True(await repository.TryAddAsync(transaction, default));

        var loaded = (await repository.GetByIdAsync(transaction.Id, default))!;
        loaded.Value.StartPayment(10m, PaymentMethod.Cash, "PAY-1");
        Assert.True(await repository.TryUpdateAsync(loaded.Value, loaded.Version, default));
        loaded = (await repository.GetByIdAsync(transaction.Id, default))!;
        loaded.Value.DeclinePayment("Card terminal offline");
        Assert.True(await repository.TryUpdateAsync(loaded.Value, loaded.Version, default));
        loaded = (await repository.GetByIdAsync(transaction.Id, default))!;
        loaded.Value.AddLineItem(EntityId.New(), "Packaging", 1, 5m);
        Assert.True(await repository.TryUpdateAsync(loaded.Value, loaded.Version, default));

        // Replaying StartPayment(10) against the new 15.00 total used to throw here.
        var reloaded = (await repository.GetByIdAsync(transaction.Id, default))!;
        var listed = Assert.Single(await repository.ListAsync(default));

        Assert.Equal(TransactionStatus.Open, reloaded.Value.Status);
        Assert.Equal(15m, reloaded.Value.Total);
        Assert.Equal(PaymentStatus.Declined, Assert.Single(reloaded.Value.Payments).Status);
        Assert.Equal("Card terminal offline", reloaded.Value.Payments[0].DeclineReason);
        Assert.Equal(transaction.Id, listed.Value.Id);

        reloaded.Value.StartPayment(15m, PaymentMethod.Cash, "PAY-2");
        Assert.True(await repository.TryUpdateAsync(reloaded.Value, reloaded.Version, default));
        Assert.Equal(TransactionStatus.PaymentPending, (await repository.GetByIdAsync(transaction.Id, default))!.Value.Status);
    }

    [Fact]
    public async Task Reload_PreservesLineAndPaymentIds()
    {
        var repository = new JsonRetailTransactionRepository(new JsonDataOptions { DataDirectory = directory });
        var transaction = new RetailTransaction(EntityId.New(), EntityId.New(), "C-201", "INR");
        var first = transaction.AddLineItem(EntityId.New(), "Counter service", 2, 10m);
        var second = transaction.AddLineItem(EntityId.New(), "Packaging", 1, 5m, TransactionLineKind.RetailProduct);
        var payment = transaction.StartPayment(25m, PaymentMethod.Card, "PAY-3");
        transaction.ConfirmPayment();
        Assert.True(await repository.TryAddAsync(transaction, default));

        var reloaded = (await repository.GetByIdAsync(transaction.Id, default))!.Value;

        Assert.Equal([first.Id, second.Id], reloaded.LineItems.Select(line => line.Id));
        Assert.Equal(TransactionLineKind.RetailProduct, reloaded.LineItems[1].Kind);
        var restoredPayment = Assert.Single(reloaded.Payments);
        Assert.Equal(payment.Id, restoredPayment.Id);
        Assert.Equal(PaymentStatus.Confirmed, restoredPayment.Status);
        Assert.Equal(TransactionStatus.Paid, reloaded.Status);
    }

    private static RetailTransaction CreateTransaction(string reference, string reason, bool reverse = false)
    {
        var transaction = new RetailTransaction(EntityId.New(), EntityId.New(), reference, "INR");
        transaction.AddLineItem(EntityId.New(), "Counter service", 1, 10m);
        transaction.StartPayment(10m, PaymentMethod.Cash, reference);
        if (reverse)
        {
            transaction.ConfirmPayment();
            transaction.ReversePayment(reason);
        }
        else
        {
            transaction.Cancel(reason);
        }
        return transaction;
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }
}
