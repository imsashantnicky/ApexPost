using ApexPost.Application.Inventory;
using ApexPost.Application.Persistence;
using ApexPost.Application.Shipping;
using ApexPost.Application.Transactions;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Pricing;
using ApexPost.Domain.Shipping;

namespace ApexPost.Infrastructure.Persistence.Json;

public sealed class JsonInventoryItemRepository : IInventoryItemRepository
{
    private readonly JsonCollectionStore<ItemData> store;
    public JsonInventoryItemRepository(JsonDataOptions options) => store = new(options.DataDirectory, "inventory.json");
    public async Task<IReadOnlyList<Versioned<InventoryItem>>> ListAsync(CancellationToken cancellationToken) => (await store.ReadAllAsync(cancellationToken)).Values.Select(ToDomain).ToArray();
    public async Task<Versioned<InventoryItem>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken) => (await store.ReadAllAsync(cancellationToken)).TryGetValue(id.Value.ToString("N"), out var x) ? ToDomain(x) : null;
    public Task<bool> TryAddAsync(InventoryItem item, CancellationToken cancellationToken) => store.TryAddAsync(item.Id.Value.ToString("N"), ToData(item, 1), cancellationToken);
    public Task<bool> TryUpdateAsync(InventoryItem item, long expectedVersion, CancellationToken cancellationToken) => store.TryReplaceAsync(item.Id.Value.ToString("N"), ToData(item, expectedVersion + 1), x => x.Version == expectedVersion, cancellationToken);
    private static Versioned<InventoryItem> ToDomain(ItemData x) => new(InventoryItem.Restore(EntityId.From(x.Id), EntityId.From(x.BranchId), InventorySku.From(x.Sku), x.Name, x.ReorderLevel, x.Movements.Select(m => (m.Type, m.Change, m.Reason, m.OccurredAt))), x.Version);
    private static ItemData ToData(InventoryItem x, long version) => new(x.Id.Value, version, x.BranchId.Value, x.Sku.Value, x.Name, x.ReorderLevel, x.Movements.Select(m => new Move(m.Type, m.QuantityChange, m.Reason, m.OccurredAt)).ToArray());
    private sealed record ItemData(Guid Id, long Version, Guid BranchId, string Sku, string Name, int ReorderLevel, Move[] Movements);
    private sealed record Move(StockMovementType Type, int Change, string Reason, DateTimeOffset OccurredAt);
}

public sealed class JsonShipmentRepository : IShipmentRepository
{
    private readonly JsonCollectionStore<Data> store;
    public JsonShipmentRepository(JsonDataOptions options) => store = new(options.DataDirectory, "shipments.json");
    public async Task<IReadOnlyList<Versioned<Shipment>>> ListAsync(CancellationToken cancellationToken) => (await store.ReadAllAsync(cancellationToken)).Values.Select(ToDomain).ToArray();
    public async Task<Versioned<Shipment>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken) => (await store.ReadAllAsync(cancellationToken)).TryGetValue(id.Value.ToString("N"), out var x) ? ToDomain(x) : null;
    public Task<bool> TryAddAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        var key = shipment.Id.Value.ToString("N");
        var value = ToData(shipment, 1);
        return store.ExecuteAtomicAsync(items =>
        {
            if (items.Values.Any(existing => existing.Number == value.Number)) return (false, false);
            var added = items.TryAdd(key, value);
            return (added, added);
        }, cancellationToken);
    }
    public Task<bool> TryUpdateAsync(Shipment shipment, long expectedVersion, CancellationToken cancellationToken) => store.TryReplaceAsync(shipment.Id.Value.ToString("N"), ToData(shipment, expectedVersion + 1), x => x.Version == expectedVersion, cancellationToken);
    private static Versioned<Shipment> ToDomain(Data x) => new(Shipment.Restore(EntityId.From(x.Id), ShipmentNumber.From(x.Number), ToParty(x.Sender), ToParty(x.Recipient), EntityId.From(x.ServiceId), new ShipmentPackage(x.Weight, x.Contents), new ShipmentCharge(x.Amount, x.Currency), x.Status, x.BranchId is Guid branchId ? EntityId.From(branchId) : null, x.ZoneCode, x.ServiceLevel), x.Version);
    private static Data ToData(Shipment x, long version) => new(x.Id.Value, version, x.ShipmentNumber.Value, ToPartyData(x.Sender), ToPartyData(x.Recipient), x.ServiceId.Value, x.Package.WeightKilograms, x.Package.ContentsDescription, x.Charge.Amount, x.Charge.CurrencyCode, x.Status, x.BranchId?.Value, x.ZoneCode, x.ServiceLevel);
    private static PartyData ToPartyData(ShipmentParty x) => new(x.Name, x.Address.Line1, x.Address.City, x.Address.PostalCode, x.Address.CountryCode);
    private static ShipmentParty ToParty(PartyData x) => new(x.Name, new ShipmentAddress(x.Line1, x.City, x.PostalCode, x.Country));
    private sealed record Data(Guid Id, long Version, string Number, PartyData Sender, PartyData Recipient, Guid ServiceId, decimal Weight, string Contents, decimal Amount, string Currency, ShipmentStatus Status, Guid? BranchId = null, string ZoneCode = "UNKNOWN", ShippingServiceLevel ServiceLevel = ShippingServiceLevel.Standard);
    private sealed record PartyData(string Name, string Line1, string City, string PostalCode, string Country);
}

public sealed class JsonServicePriceLookup : IServicePriceLookup
{
    private readonly JsonCollectionStore<Price> prices;
    private readonly JsonCollectionStore<Service> services;
    public JsonServicePriceLookup(JsonDataOptions options) { prices = new(options.DataDirectory, "pricing.json"); services = new(options.DataDirectory, "catalogue.json"); }
    public async Task<ServicePrice?> GetCurrentPriceAsync(EntityId serviceId, string currencyCode, CancellationToken cancellationToken)
    {
        var catalogue = await services.ReadAllAsync(cancellationToken);
        if (!catalogue.TryGetValue(serviceId.Value.ToString("N"), out var service) || service.Availability != ApexPost.Domain.Catalogue.ServiceAvailability.Available) return null;
        var price = (await prices.ReadAllAsync(cancellationToken)).Values.SingleOrDefault(x => x.ServiceCode == service.Code && x.CurrencyCode == currencyCode);
        return price is null ? null : ServicePrice.From(price.Amount, price.CurrencyCode);
    }
    private sealed record Price(string ServiceCode, decimal Amount, string CurrencyCode);
    private sealed record Service(Guid Id, long Version, string Code, string Name, string Description, ApexPost.Domain.Catalogue.ServiceAvailability Availability);
}

public sealed class JsonShippingRateLookup : IShippingRateLookup
{
    private readonly JsonCollectionStore<ShippingRateData> rates;

    public JsonShippingRateLookup(JsonDataOptions options) => rates = new(options.DataDirectory, "shipping-rates.json");

    public async Task<decimal?> GetCurrentRateAsync(string zoneCode, ShippingServiceLevel serviceLevel, string currencyCode, CancellationToken cancellationToken)
    {
        var key = $"{zoneCode.Trim().ToUpperInvariant()}|{serviceLevel}|{currencyCode.Trim().ToUpperInvariant()}";
        var items = await rates.ReadAllAsync(cancellationToken);
        return items.TryGetValue(key, out var rate) ? rate.Amount : null;
    }

    private sealed record ShippingRateData(string ZoneCode, ShippingServiceLevel ServiceLevel, decimal Amount, string CurrencyCode);
}
