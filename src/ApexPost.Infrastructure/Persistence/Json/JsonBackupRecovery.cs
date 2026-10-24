using System.Text.Json;

namespace ApexPost.Infrastructure.Persistence.Json;

/// <summary>Creates and restores operator-controlled JSON backups; it never restores automatically.</summary>
public sealed class JsonBackupRecovery
{
    public static async Task<string> CreateBackupAsync(string dataFilePath, CancellationToken cancellationToken)
    {
        var source = Path.GetFullPath(dataFilePath);
        if (!File.Exists(source)) throw new FileNotFoundException("JSON data file was not found.", source);
        var backup = $"{source}.bak";
        try
        {
            await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            await using var output = new FileStream(backup, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
            await input.CopyToAsync(output, cancellationToken);
            return backup;
        }
        catch (IOException exception) { throw new JsonPersistenceException($"Backup for '{source}' could not be created.", exception); }
    }

    public static async Task RestoreBackupAsync(string dataFilePath, CancellationToken cancellationToken)
    {
        var destination = Path.GetFullPath(dataFilePath);
        var backup = $"{destination}.bak";
        if (!File.Exists(backup)) throw new FileNotFoundException("JSON backup file was not found.", backup);
        try
        {
            var content = await File.ReadAllTextAsync(backup, cancellationToken);
            if (string.IsNullOrWhiteSpace(content)) throw new JsonException("Backup is empty.");
            JsonDocument.Parse(content).Dispose();
            var temporary = $"{destination}.{Guid.NewGuid():N}.restore.tmp";
            await File.WriteAllTextAsync(temporary, content, cancellationToken);
            File.Move(temporary, destination, true);
        }
        catch (JsonException exception) { throw new JsonPersistenceException($"Backup for '{destination}' is malformed and was not restored.", exception); }
        catch (IOException exception) { throw new JsonPersistenceException($"Backup for '{destination}' could not be restored.", exception); }
    }
}
