using ApexPost.Infrastructure.Persistence.Json;

namespace ApexPost.Infrastructure.Tests;

public class JsonSeedInitializerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ApexPostSeedTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task EnabledSeedingInitializesAnEmptyDirectory()
    {
        await Initializer(enabled: true).InitializeAsync(default);

        Assert.Equal(4, Directory.GetFiles(directory, "*.json").Length);
        Assert.Contains("BLR-001", await File.ReadAllTextAsync(Path.Combine(directory, "branches.json")));
        Assert.DoesNotContain("password", await File.ReadAllTextAsync(Path.Combine(directory, "staff.json")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RepeatedInitializationPreservesSeededFiles()
    {
        var initializer = Initializer(enabled: true);
        await initializer.InitializeAsync(default);
        var before = await File.ReadAllTextAsync(Path.Combine(directory, "catalogue.json"));

        await initializer.InitializeAsync(default);

        Assert.Equal(before, await File.ReadAllTextAsync(Path.Combine(directory, "catalogue.json")));
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
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "branches.json");
        const string existing = "{\"custom\":{\"name\":\"User branch\"}}";
        await File.WriteAllTextAsync(path, existing);

        await Initializer(enabled: true).InitializeAsync(default);

        Assert.Equal(existing, await File.ReadAllTextAsync(path));
        Assert.Single(Directory.GetFiles(directory, "*.json"));
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

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }
}
