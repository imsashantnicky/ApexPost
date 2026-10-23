using ApexPost.Domain.Common;
using ApexPost.Domain.Pricing;

namespace ApexPost.Application.Transactions;

/// <summary>Supplies the authoritative local price for an application transaction line.</summary>
public interface IServicePriceLookup
{
    Task<ServicePrice?> GetCurrentPriceAsync(EntityId serviceId, string currencyCode, CancellationToken cancellationToken);
}
