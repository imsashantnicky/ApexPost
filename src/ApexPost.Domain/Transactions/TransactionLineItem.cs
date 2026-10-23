using ApexPost.Domain.Common;

namespace ApexPost.Domain.Transactions;

public sealed class TransactionLineItem
{
    internal TransactionLineItem(EntityId id, EntityId serviceId, string description, int quantity, decimal unitPrice)
    {
        Id = id;
        ServiceId = serviceId;
        Description = RequiredDescription(description);
        Quantity = ValidQuantity(quantity);
        UnitPrice = ValidUnitPrice(unitPrice);
    }

    public EntityId Id { get; }

    public EntityId ServiceId { get; }

    public string Description { get; }

    public int Quantity { get; }

    public decimal UnitPrice { get; }

    public decimal Total => Quantity * UnitPrice;

    private static string RequiredDescription(string description)
    {
        var normalizedDescription = description?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedDescription) || normalizedDescription.Length > 250)
        {
            throw new DomainException("Transaction line description is required and must not exceed 250 characters.");
        }

        return normalizedDescription;
    }

    private static int ValidQuantity(int quantity)
    {
        if (quantity < 1)
        {
            throw new DomainException("Transaction line quantity must be greater than zero.");
        }

        return quantity;
    }

    private static decimal ValidUnitPrice(decimal unitPrice)
    {
        if (unitPrice <= 0 || decimal.Round(unitPrice, 2) != unitPrice)
        {
            throw new DomainException("Transaction line unit price must be greater than zero and have no more than two decimal places.");
        }

        return unitPrice;
    }
}
