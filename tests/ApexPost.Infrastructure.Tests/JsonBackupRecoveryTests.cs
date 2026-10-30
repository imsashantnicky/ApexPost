using ApexPost.Infrastructure.Persistence.Json;

namespace ApexPost.Infrastructure.Tests;

public class JsonBackupRecoveryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ApexPostBackupTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task BackupAndExplicitRecoveryRestoreValidPreviousData()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "items.json");
        const string original = "{\"one\":{\"name\":\"first\"}}";
        await File.WriteAllTextAsync(path, original);
        await JsonBackupRecovery.CreateBackupAsync(path, default);
        await File.WriteAllTextAsync(path, "{\"two\":{\"name\":\"second\"}}");

        await JsonBackupRecovery.RestoreBackupAsync(path, default);

        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task MalformedBackupDoesNotReplaceCurrentData()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "items.json");
        const string current = "{\"current\":true}";
        await File.WriteAllTextAsync(path, current);
        await File.WriteAllTextAsync($"{path}.bak", "{ invalid");

        await Assert.ThrowsAsync<JsonPersistenceException>(() => JsonBackupRecovery.RestoreBackupAsync(path, default));

        Assert.Equal(current, await File.ReadAllTextAsync(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        GC.SuppressFinalize(this);
    }
}
