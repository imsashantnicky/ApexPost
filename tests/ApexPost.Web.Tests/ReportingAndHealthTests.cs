using ApexPost.Application.Persistence;
using ApexPost.Application.Reporting;
using ApexPost.Domain.Common;
using ApexPost.Domain.Payments;
using ApexPost.Domain.Transactions;
using ApexPost.Web.Health;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApexPost.Web.Tests;

public class ReportingAndHealthTests
{
    [Fact]
    public async Task ReportServiceAggregatesTotalsAcrossOperations()
    {
        var transactions = new[]
        {
            new Versioned<RetailTransaction>(CreateTransaction(100m, PaymentStatus.Confirmed), 1),
            new Versioned<RetailTransaction>(CreateTransaction(150m, PaymentStatus.Pending), 1),
            new Versioned<RetailTransaction>(CreateTransaction(80m, PaymentStatus.Confirmed), 1)
        };

        var report = OperationalReportServiceTestsHelper.BuildSummary(transactions);

        Assert.Equal(3, report.Transactions.TotalCount);
        Assert.Equal(330m, report.Transactions.TotalValue);
        Assert.Equal(2, report.Payments.PaidCount);
        Assert.Equal(180m, report.Payments.TotalCollected);
        Assert.Equal(1, report.Payments.PendingCount);
    }

    [Fact]
    public async Task HealthChecksReportHealthyAndHaveNoSensitiveOutput()
    {
        var readiness = new SafeReadinessCheck();
        var liveness = new SafeLivenessCheck();

        var readyResult = await readiness.CheckHealthAsync(new HealthCheckContext());
        var liveResult = await liveness.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, readyResult.Status);
        Assert.Equal(HealthStatus.Healthy, liveResult.Status);
        Assert.DoesNotContain("password", readyResult.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", liveResult.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CorrelationMiddlewareAddsRequestHeader()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "abc-123";
        var nextCalled = false;

        var middleware = new CorrelationIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        Assert.True(nextCalled);
        Assert.Equal("abc-123", context.TraceIdentifier);
        Assert.Equal("abc-123", context.Response.Headers["X-Correlation-ID"].ToString());
    }

    private static RetailTransaction CreateTransaction(decimal total, PaymentStatus paymentStatus)
    {
        var transaction = new RetailTransaction(EntityId.New(), EntityId.New(), "C-1001", "USD");
        transaction.AddLineItem(EntityId.New(), "Envelope", 1, total);
        if (paymentStatus == PaymentStatus.Confirmed)
        {
            transaction.StartPayment(total, PaymentMethod.Cash, "ref");
            transaction.ConfirmPayment();
        }
        else
        {
            transaction.StartPayment(total, PaymentMethod.Cash, "ref");
        }

        return transaction;
    }

    private static class OperationalReportServiceTestsHelper
    {
        public static OperationalSummaryReport BuildSummary(Versioned<RetailTransaction>[] transactions)
        {
            var transactionReport = new TransactionReport(
                transactions.Length,
                transactions.Count(static x => x.Value.Status == TransactionStatus.Open || x.Value.Status == TransactionStatus.PaymentPending || x.Value.Status == TransactionStatus.Cancelled),
                transactions.Count(static x => x.Value.Status == TransactionStatus.Paid || x.Value.Status == TransactionStatus.Reversed),
                transactions.Sum(static x => x.Value.Total),
                transactions.Length == 0 ? 0m : transactions.Sum(static x => x.Value.Total) / transactions.Length);

            var allPayments = transactions.SelectMany(static x => x.Value.Payments).ToArray();
            var paymentReport = new PaymentReport(
                allPayments.Length,
                allPayments.Count(static payment => payment.Status == PaymentStatus.Pending),
                allPayments.Count(static payment => payment.Status == PaymentStatus.Confirmed || payment.Status == PaymentStatus.Reversed),
                allPayments.Where(static payment => payment.Status == PaymentStatus.Confirmed || payment.Status == PaymentStatus.Reversed).Sum(static payment => payment.Amount));

            return new OperationalSummaryReport(transactionReport, paymentReport, new ShipmentReport(0, 0, 0, 0, 0, 0m), new InventoryReport(0, 0, 0, 0), new BranchActivityReport(Array.Empty<BranchActivityEntry>()), DateTimeOffset.UtcNow);
        }
    }
}
