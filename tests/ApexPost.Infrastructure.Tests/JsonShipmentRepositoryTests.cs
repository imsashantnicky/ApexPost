using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;
using ApexPost.Infrastructure.Persistence.Json;

namespace ApexPost.Infrastructure.Tests;

public sealed class JsonShipmentRepositoryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ApexPostShipments", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ConcurrentDuplicateShipmentNumbersAllowOnlyOneWrite()
    {
        var repository = new JsonShipmentRepository(new JsonDataOptions { DataDirectory = directory });
        var attempts = Enumerable.Range(0, 16)
            .Select(_ => repository.TryAddAsync(CreateShipment("AA-123456"), CancellationToken.None));

        var results = await Task.WhenAll(attempts);

        Assert.Single(results, result => result);
        Assert.Single(await repository.ListAsync(CancellationToken.None));
    }

    private static Shipment CreateShipment(string number) => new(
        EntityId.New(), ShipmentNumber.From(number),
        new ShipmentParty("Sender", new ShipmentAddress("1 Sender Road", "Pune", "411001", "IN")),
        new ShipmentParty("Recipient", new ShipmentAddress("2 Recipient Road", "Mumbai", "400001", "IN")),
        EntityId.New(), new ShipmentPackage(1m, "Documents"), new ShipmentCharge(100m, "INR"), EntityId.New());

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }
}
