using System.Text.Json;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Staff;

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

        foreach (var branch in SeedData.Branches) await branches.TryAddAsync(branch.Id.ToString("N"), branch, cancellationToken);
        foreach (var staffMember in SeedData.Staff) await staff.TryAddAsync(staffMember.Id.ToString("N"), staffMember, cancellationToken);
        foreach (var service in SeedData.Services) await catalogue.TryAddAsync(service.Id.ToString("N"), service, cancellationToken);
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
        public static readonly SeedBranch[] Branches = [new(Guid.Parse("11111111-1111-1111-1111-111111111111"), 1, "BLR-001", "Bengaluru Central", "1 Market Road", null, "Bengaluru", "560001", "IN", 3, BranchStatus.Active), new(Guid.Parse("22222222-2222-2222-2222-222222222222"), 1, "MYS-001", "Mysuru Central", "2 Lake Road", null, "Mysuru", "570001", "IN", 2, BranchStatus.Active)];
        public static readonly SeedStaff[] Staff = [new(Guid.Parse("33333333-3333-3333-3333-333333333333"), 1, "EMP-0001", "Anika Sharma", Branches[0].Id, [StaffRole.BranchManager], EmploymentStatus.Active), new(Guid.Parse("44444444-4444-4444-4444-444444444444"), 1, "EMP-0002", "Rahul Verma", Branches[1].Id, [StaffRole.CounterClerk], EmploymentStatus.Active)];
        public static readonly SeedService[] Services = [new(Guid.Parse("55555555-5555-5555-5555-555555555555"), 1, "EXP-001", "Express Parcel", "Priority parcel delivery.", ServiceAvailability.Available), new(Guid.Parse("66666666-6666-6666-6666-666666666666"), 1, "REG-001", "Registered Post", "Tracked standard delivery.", ServiceAvailability.Available)];
        public static readonly SeedPrice[] Prices = [new("EXP-001", 125.50m, "INR"), new("REG-001", 45.00m, "INR")];
    }

    private sealed record SeedBranch(Guid Id, long Version, string Code, string Name, string Line1, string? Line2, string City, string PostalCode, string CountryCode, int CounterCount, BranchStatus Status);
    private sealed record SeedStaff(Guid Id, long Version, string EmployeeNumber, string Name, Guid BranchId, StaffRole[] Roles, EmploymentStatus Status);
    private sealed record SeedService(Guid Id, long Version, string Code, string Name, string Description, ServiceAvailability Availability);
    private sealed record SeedPrice(string ServiceCode, decimal Amount, string CurrencyCode);
}
