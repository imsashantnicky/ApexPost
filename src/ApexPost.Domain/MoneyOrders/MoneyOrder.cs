using ApexPost.Domain.Common;

namespace ApexPost.Domain.MoneyOrders;

public sealed class MoneyOrder
{
    public MoneyOrder(
        EntityId id,
        MoneyOrderNumber orderNumber,
        MoneyOrderAmount amount,
        EntityId originatingBranchId,
        MoneyOrderBeneficiary beneficiary,
        EntityId? identityVerificationId = null)
    {
        Id = id ?? throw new DomainException("Money order identity is required.");
        OrderNumber = orderNumber ?? throw new DomainException("Money order number is required.");
        Amount = amount ?? throw new DomainException("Money order amount is required.");
        OriginatingBranchId = originatingBranchId ?? throw new DomainException("Originating branch identity is required.");
        Beneficiary = beneficiary ?? throw new DomainException("Money order beneficiary is required.");
        IdentityVerificationId = identityVerificationId;
        Status = MoneyOrderStatus.Issued;
    }

    public EntityId Id { get; }

    public MoneyOrderNumber OrderNumber { get; }

    public MoneyOrderAmount Amount { get; }

    public EntityId OriginatingBranchId { get; }

    public MoneyOrderBeneficiary Beneficiary { get; }

    public EntityId? IdentityVerificationId { get; }

    public MoneyOrderStatus Status { get; private set; }

    public void MarkPaid()
    {
        ChangeStatus(MoneyOrderStatus.Paid, "Only an issued money order can be paid.");
    }

    public void Cancel()
    {
        ChangeStatus(MoneyOrderStatus.Cancelled, "Only an issued money order can be cancelled.");
    }

    public void Expire()
    {
        ChangeStatus(MoneyOrderStatus.Expired, "Only an issued money order can expire.");
    }

    public static MoneyOrder Restore(EntityId id, MoneyOrderNumber orderNumber, MoneyOrderAmount amount, EntityId originatingBranchId, MoneyOrderBeneficiary beneficiary, MoneyOrderStatus status, EntityId? identityVerificationId = null)
    {
        var order = new MoneyOrder(id, orderNumber, amount, originatingBranchId, beneficiary, identityVerificationId);
        if (status == MoneyOrderStatus.Paid) order.MarkPaid();
        else if (status == MoneyOrderStatus.Cancelled) order.Cancel();
        else if (status == MoneyOrderStatus.Expired) order.Expire();
        else if (status != MoneyOrderStatus.Issued) throw new DomainException("Money order status must be recognized.");
        return order;
    }

    private void ChangeStatus(MoneyOrderStatus targetStatus, string errorMessage)
    {
        if (Status != MoneyOrderStatus.Issued)
        {
            throw new DomainException(errorMessage);
        }

        Status = targetStatus;
    }
}
