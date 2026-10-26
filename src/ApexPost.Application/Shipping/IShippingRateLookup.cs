using ApexPost.Domain.Shipping;

namespace ApexPost.Application.Shipping;

public interface IShippingRateLookup
{
    Task<decimal?> GetCurrentRateAsync(string zoneCode, ShippingServiceLevel serviceLevel, string currencyCode, CancellationToken cancellationToken);
}
