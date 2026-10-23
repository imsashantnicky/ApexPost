using ApexPost.Domain.Payments;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Transactions;

public sealed record CreateTransactionRequest(Guid BranchId, string CounterReference, string CurrencyCode);
public sealed record AddTransactionLineRequest(Guid TransactionId, long ExpectedVersion, Guid ServiceId, string Description, int Quantity);
public sealed record StartPaymentRequest(Guid TransactionId, long ExpectedVersion, decimal Amount, PaymentMethod Method, string Reference, string IdempotencyKey);
public sealed record TransactionResponse(Guid Id, long Version, Guid BranchId, string CounterReference, string CurrencyCode, decimal Total, TransactionStatus Status, IReadOnlyList<TransactionLineResponse> Lines, IReadOnlyList<PaymentResponse> Payments);
public sealed record TransactionLineResponse(Guid Id, Guid ServiceId, string Description, int Quantity, decimal UnitPrice, decimal Total);
public sealed record PaymentResponse(Guid Id, decimal Amount, string CurrencyCode, PaymentMethod Method, string Reference, PaymentStatus Status);
public sealed record ReceiptResponse(string ReceiptNumber, Guid TransactionId, string CounterReference, decimal Total, string CurrencyCode, IReadOnlyList<TransactionLineResponse> Lines, PaymentResponse Payment);
