using ApexPost.Domain.Common;

namespace ApexPost.Domain.Transactions;

/// <summary>Persisted state of a transaction line, used only to restore an existing transaction.</summary>
public sealed record TransactionLineState(EntityId Id, EntityId ServiceId, string Description, int Quantity, decimal UnitPrice, TransactionLineKind Kind = TransactionLineKind.Service);
