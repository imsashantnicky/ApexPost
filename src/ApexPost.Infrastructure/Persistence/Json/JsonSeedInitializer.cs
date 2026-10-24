using System.Text.Json;

namespace ApexPost.Infrastructure.Persistence.Json;

public sealed class JsonSeedInitializer
{
    private readonly JsonDataOptions dataOptions;
    private readonly JsonSeedOptions seedOptions;

    public JsonSeedInitializer(JsonDataOptions dataOptions, JsonSeedOptions seedOptions)
    {
        this.dataOptions = dataOptions ?? throw new ArgumentNullException(nameof(dataOptions));
        this.seedOptions = seedOptions ?? throw new ArgumentNullException(nameof(seedOptions));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (!seedOptions.Enabled) return;
        var directory = Path.GetFullPath(dataOptions.DataDirectory);
        if (Directory.Exists(directory))
        {
            var existingFiles = Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).ToArray();
            if (existingFiles.Length > 0)
            {
                await ValidateExistingFilesAsync(existingFiles, cancellationToken);
                return;
            }
        }

        var branches = new JsonCollectionStore<SeedBranch>(directory, "branches.json");
        var staff = new JsonCollectionStore<SeedStaff>(directory, "staff.json");
        var catalogue = new JsonCollectionStore<SeedService>(directory, "catalogue.json");
        var pricing = new JsonCollectionStore<SeedPrice>(directory, "pricing.json");

        foreach (var branch in SeedData.Branches) await branches.TryAddAsync(branch.Code, branch, cancellationToken);
        foreach (var staffMember in SeedData.Staff) await staff.TryAddAsync(staffMember.EmployeeNumber, staffMember, cancellationToken);
        foreach (var service in SeedData.Services) await catalogue.TryAddAsync(service.Code, service, cancellationToken);
        foreach (var price in SeedData.Prices) await pricing.TryAddAsync(price.ServiceCode, price, cancellationToken);
    }

    private static async Task ValidateExistingFilesAsync(IEnumerable<string> files, CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file, cancellationToken);
                if (!string.IsNullOrWhiteSpace(content)) JsonDocument.Parse(content).Dispose();
            }
            catch (JsonException exception)
            {
                throw new JsonPersistenceException($"Existing JSON file '{file}' is malformed; initialization was stopped.", exception);
            }
            catch (IOException exception)
            {
                throw new JsonPersistenceException($"Existing JSON file '{file}' could not be read; initialization was stopped.", exception);
            }
        }
    }

    private static class SeedData
    {
        public static readonly SeedBranch[] Branches = [new("BLR-001", "Bengaluru Central", "1 Market Road", "Bengaluru", "560001", "IN", 3), new("MYS-001", "Mysuru Central", "2 Lake Road", "Mysuru", "570001", "IN", 2)];
        public static readonly SeedStaff[] Staff = [new("EMP-0001", "Anika Sharma", "BLR-001", "BranchManager"), new("EMP-0002", "Rahul Verma", "MYS-001", "CounterClerk")];
        public static readonly SeedService[] Services = [new("EXP-001", "Express Parcel", "Priority parcel delivery."), new("REG-001", "Registered Post", "Tracked standard delivery.")];
        public static readonly SeedPrice[] Prices = [new("EXP-001", 125.50m, "INR"), new("REG-001", 45.00m, "INR")];
    }

    private sealed record SeedBranch(string Code, string Name, string AddressLine1, string City, string PostalCode, string CountryCode, int CounterCount);
    private sealed record SeedStaff(string EmployeeNumber, string Name, string BranchCode, string Role);
    private sealed record SeedService(string Code, string Name, string Description);
    private sealed record SeedPrice(string ServiceCode, decimal Amount, string CurrencyCode);
}
