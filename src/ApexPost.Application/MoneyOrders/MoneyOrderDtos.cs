using ApexPost.Domain.MoneyOrders;

namespace ApexPost.Application.MoneyOrders;

public sealed record IssueMoneyOrderRequest(string OrderNumber, decimal Amount, string CurrencyCode, Guid OriginatingBranchId, string BeneficiaryName, string BeneficiaryReference);
public sealed record MoneyOrderResponse(Guid Id, long Version, string OrderNumber, decimal Amount, string CurrencyCode, Guid OriginatingBranchId, string BeneficiaryName, string BeneficiaryReference, MoneyOrderStatus Status);
