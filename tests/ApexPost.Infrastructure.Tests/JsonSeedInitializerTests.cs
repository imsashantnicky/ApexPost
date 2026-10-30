using System.Text.Json;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.Staff;
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
    public async Task Seed_DoesNotDuplicateBusinessKeys()
    {
        // Legacy App_Data rows that reuse seed business keys under different GUIDs.
        var options = new JsonDataOptions { DataDirectory = directory };
        var legacyBlr = EntityId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var legacyMys = EntityId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var branches = new JsonBranchRepository(options);
        Assert.True(await branches.TryAddAsync(new Branch(legacyBlr, BranchCode.From("BLR-001"), "Legacy Bengaluru", new BranchAddress("1 Road", null, "Bengaluru", "560001", "IN"), 1), default));
        Assert.True(await branches.TryAddAsync(new Branch(legacyMys, BranchCode.From("MYS-001"), "Legacy Mysuru", new BranchAddress("2 Road", null, "Mysuru", "570001", "IN"), 1), default));
        var staff = new JsonStaffRepository(options);
        Assert.True(await staff.TryAddAsync(new StaffMember(EntityId.From(Guid.Parse("33333333-3333-3333-3333-333333333333")), EmployeeNumber.From("EMP-0001"), "Legacy One", legacyBlr, [StaffRole.BranchManager]), default));
        Assert.True(await staff.TryAddAsync(new StaffMember(EntityId.From(Guid.Parse("44444444-4444-4444-4444-444444444444")), EmployeeNumber.From("EMP-0002"), "Legacy Two", legacyMys, [StaffRole.CounterClerk]), default));
        var catalogue = new JsonServiceDefinitionRepository(options);
        Assert.True(await catalogue.TryAddAsync(new ServiceDefinition(EntityId.New(), ServiceCode.From("EXP-001"), "Legacy Express", "Legacy"), default));
        Assert.True(await catalogue.TryAddAsync(new ServiceDefinition(EntityId.New(), ServiceCode.From("REG-001"), "Legacy Registered", "Legacy"), default));

        await Initializer(enabled: true).InitializeAsync(default);
        await Initializer(enabled: true).InitializeAsync(default);

        var storedBranches = await branches.ListAsync(default);
        var storedStaff = await staff.ListAsync(default);
        var storedServices = await catalogue.ListAsync(default);
        var storedInventory = await new JsonInventoryItemRepository(options).ListAsync(default);
        AssertUnique(storedBranches.Select(x => x.Value.Code.Value));
        AssertUnique(storedStaff.Select(x => x.Value.EmployeeNumber.Value));
        AssertUnique(storedServices.Select(x => x.Value.Code.Value));
        AssertUnique(storedInventory.Select(x => $"{x.Value.BranchId.Value:N}|{x.Value.Sku.Value}"));
        Assert.Equal(15, storedBranches.Count);
        Assert.Equal(20, storedStaff.Count);
        Assert.Equal(30, storedServices.Count);

        // Seeded rows that reference a branch or service point at the ids actually stored for that key.
        var branchIds = storedBranches.Select(x => x.Value.Id).ToHashSet();
        var serviceIds = storedServices.Select(x => x.Value.Id).ToHashSet();
        Assert.Contains(legacyBlr, branchIds);
        Assert.All(storedStaff, x => Assert.Contains(x.Value.BranchId, branchIds));
        Assert.All(storedInventory, x => Assert.Contains(x.Value.BranchId, branchIds));
        Assert.All(await new JsonShipmentRepository(options).ListAsync(default), x => Assert.Contains(x.Value.ServiceId, serviceIds));
        Assert.All(await new JsonRetailTransactionRepository(options).ListAsync(default), x => Assert.Contains(x.Value.BranchId, branchIds));
    }

    [Fact]
    public async Task InventoryRepository_RejectsDuplicateSkuWithinBranch()
    {
        var repository = new JsonInventoryItemRepository(new JsonDataOptions { DataDirectory = directory });
        var branch = EntityId.New();

        Assert.True(await repository.TryAddAsync(new InventoryItem(EntityId.New(), branch, InventorySku.From("BOX-001"), "Box", 1), default));
        Assert.False(await repository.TryAddAsync(new InventoryItem(EntityId.New(), branch, InventorySku.From("BOX-001"), "Box again", 1), default));
        Assert.True(await repository.TryAddAsync(new InventoryItem(EntityId.New(), EntityId.New(), InventorySku.From("BOX-001"), "Box elsewhere", 1), default));
    }

    private static void AssertUnique(IEnumerable<string> keys)
    {
        var duplicates = keys.GroupBy(key => key, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        Assert.True(duplicates.Length == 0, $"Duplicate business keys: {string.Join(", ", duplicates)}");
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
