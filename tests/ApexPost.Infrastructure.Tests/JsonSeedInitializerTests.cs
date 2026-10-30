using System.Text.Json;
using ApexPost.Infrastructure.Persistence.Json;

namespace ApexPost.Infrastructure.Tests;

public class JsonSeedInitializerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ApexPostSeedTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task EnabledSeedingInitializesAnEmptyDirectory()
    {
        await Initializer(enabled: true).InitializeAsync(default);

        Assert.Equal(10, Directory.GetFiles(directory, "*.json").Length);
        Assert.Equal(15, await CountAsync("branches.json"));
        Assert.Equal(20, await CountAsync("staff.json"));
        Assert.Equal(30, await CountAsync("catalogue.json"));
        Assert.Equal(30, await CountAsync("pricing.json"));
        Assert.Equal(30, await CountAsync("inventory.json"));
        Assert.Equal(20, await CountAsync("shipments.json"));
        Assert.Equal(30, await CountAsync("shipping-rates.json"));
        Assert.Equal(40, await CountAsync("transactions.json"));
        Assert.Equal(16, await CountAsync("money-orders.json"));
        Assert.Equal(12, await CountAsync("identity-verifications.json"));
        Assert.DoesNotContain("password", await File.ReadAllTextAsync(Path.Combine(directory, "staff.json")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RepeatedInitializationPreservesSeededFiles()
    {
        var initializer = Initializer(enabled: true);
        await initializer.InitializeAsync(default);
        var before = Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal)
            .ToDictionary(path => Path.GetFileName(path), File.ReadAllText);

        await initializer.InitializeAsync(default);

        foreach (var (fileName, content) in before)
            Assert.Equal(content, await File.ReadAllTextAsync(Path.Combine(directory, fileName)));
    }

    [Fact]
    public async Task DisabledSeedingDoesNotCreateDirectoryOrFiles()
    {
        await Initializer(enabled: false).InitializeAsync(default);

        Assert.False(Directory.Exists(directory));
    }


[Fact]
public async Task ExistingRecordsArePreserved()
{
    var initializer = Initializer(enabled: true);
    await initializer.InitializeAsync(default);

    var path = Path.Combine(directory, "branches.json");

    var records = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
        await File.ReadAllTextAsync(path))!;

    var firstRecord = records.First();

    var singleRecordJson = JsonSerializer.Serialize(
        new Dictionary<string, JsonElement>
        {
            [firstRecord.Key] = firstRecord.Value
        });

    await File.WriteAllTextAsync(path, singleRecordJson);

    JsonElement preservedValue;

    using (var beforeDocument = JsonDocument.Parse(
        await File.ReadAllTextAsync(path)))
    {
        preservedValue = beforeDocument.RootElement
            .GetProperty(firstRecord.Key)
            .Clone();
    }

    foreach (var file in new[]
    {
        "staff.json",
        "catalogue.json",
        "pricing.json"
    })
    {
        File.Delete(Path.Combine(directory, file));
    }

    await initializer.InitializeAsync(default);

    using var restoredDocument = JsonDocument.Parse(
        await File.ReadAllTextAsync(path));

    var restored = restoredDocument.RootElement;

    Assert.Equal(15, restored.EnumerateObject().Count());

    Assert.True(
        JsonElement.DeepEquals(
            preservedValue,
            restored.GetProperty(firstRecord.Key)),
        "The existing branch record must retain its JSON value.");

    Assert.Equal(20, await CountAsync("staff.json"));
    Assert.Equal(30, await CountAsync("catalogue.json"));
    Assert.Equal(30, await CountAsync("pricing.json"));
}

    [Fact]
    public async Task CorruptExistingDataStopsInitializationWithoutReplacingIt()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "branches.json");
        await File.WriteAllTextAsync(path, "{ invalid");

        await Assert.ThrowsAsync<JsonPersistenceException>(() => Initializer(enabled: true).InitializeAsync(default));

        Assert.Equal("{ invalid", await File.ReadAllTextAsync(path));
    }

    private JsonSeedInitializer Initializer(bool enabled) => new(new JsonDataOptions { DataDirectory = directory }, new JsonSeedOptions { Enabled = enabled });

    private async Task<int> CountAsync(string fileName)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, fileName)));
        return document.RootElement.EnumerateObject().Count();
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }
}
