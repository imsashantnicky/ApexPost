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
