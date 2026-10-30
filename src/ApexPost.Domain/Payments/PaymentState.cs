using ApexPost.Domain.Common;

namespace ApexPost.Domain.Payments;

/// <summary>Persisted state of a payment attempt, used only to restore an existing transaction.</summary>
public sealed record PaymentState(EntityId Id, decimal Amount, string CurrencyCode, PaymentMethod Method, string Reference, PaymentStatus Status, string? DeclineReason = null, string? ReversalReason = null);
