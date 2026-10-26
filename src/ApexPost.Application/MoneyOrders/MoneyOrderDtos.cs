using ApexPost.Domain.MoneyOrders;

namespace ApexPost.Application.MoneyOrders;

public sealed record IssueMoneyOrderRequest(string OrderNumber, decimal Amount, string CurrencyCode, Guid OriginatingBranchId, string BeneficiaryName, string BeneficiaryReference, Guid? IdentityVerificationId = null);
public sealed record MoneyOrderResponse(Guid Id, long Version, string OrderNumber, decimal Amount, string CurrencyCode, Guid OriginatingBranchId, string BeneficiaryName, string BeneficiaryReference, MoneyOrderStatus Status, Guid? IdentityVerificationId = null);
