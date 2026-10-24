
using System.Text.Json;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Staff;

namespace ApexPost.Infrastructure.Persistence.Json;

public sealed class JsonSeedInitializer
{
    private readonly JsonDataOptions dataOptions;
    private readonly JsonSeedOptions seedOptions;

    public JsonSeedInitializer(
        JsonDataOptions dataOptions,
        JsonSeedOptions seedOptions)
    {
        this.dataOptions = dataOptions
            ?? throw new ArgumentNullException(nameof(dataOptions));

        this.seedOptions = seedOptions
            ?? throw new ArgumentNullException(nameof(seedOptions));
    }

    public async Task InitializeAsync(
        CancellationToken cancellationToken)
    {
        if (!seedOptions.Enabled)
            return;

        var directory = Path.GetFullPath(dataOptions.DataDirectory);
        Directory.CreateDirectory(directory);

        // Do not overwrite an existing business dataset.
        // accounts.json is deliberately excluded from seeding.
        var existingBusinessFiles = Directory
            .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(file => !string.Equals(
                Path.GetFileName(file),
                "accounts.json",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (existingBusinessFiles.Length > 0)
        {
            await ValidateExistingFilesAsync(
                existingBusinessFiles,
                cancellationToken);
        }


        var branches = new JsonCollectionStore<SeedBranch>(
            directory, "branches.json");

        var staff = new JsonCollectionStore<SeedStaff>(
            directory, "staff.json");

        var catalogue = new JsonCollectionStore<SeedService>(
            directory, "catalogue.json");

        var pricing = new JsonCollectionStore<SeedPrice>(
            directory, "pricing.json");

        foreach (var item in SeedData.Branches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await branches.TryAddAsync(
                item.Id.ToString("N"), item, cancellationToken);
        }

        foreach (var item in SeedData.Staff)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await staff.TryAddAsync(
                item.Id.ToString("N"), item, cancellationToken);
        }

        foreach (var item in SeedData.Services)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await catalogue.TryAddAsync(
                item.Id.ToString("N"), item, cancellationToken);
        }

        foreach (var item in SeedData.Prices)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await pricing.TryAddAsync(
                item.ServiceCode, item, cancellationToken);
        }
    }

    private static async Task ValidateExistingFilesAsync(
        IEnumerable<string> files,
        CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(
                    file, cancellationToken);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    using var document = JsonDocument.Parse(content);
                }
            }
            catch (JsonException exception)
            {
                throw new JsonPersistenceException(
                    $"Existing JSON file '{file}' is malformed; " +
                    "initialization was stopped.",
                    exception);
            }
            catch (IOException exception)
            {
                throw new JsonPersistenceException(
                    $"Existing JSON file '{file}' could not be read; " +
                    "initialization was stopped.",
                    exception);
            }
        }
    }

    private static class SeedData
    {
        private sealed record BranchInput(
            string Code,
            string Name,
            string Address,
            string City,
            string PostalCode,
            int CounterCount);

        private sealed record StaffInput(
            string EmployeeNumber,
            string Name,
            int BranchIndex,
            StaffRole[] Roles);

        private sealed record ServiceInput(
            string Code,
            string Name,
            string Description,
            decimal Price);

        private static Guid StableId(int group, int number) =>
            Guid.Parse(
                $"{group:D8}-0000-0000-0000-{number:D12}");

        private static readonly BranchInput[] BranchInputs =
        [
            new("BLR-001", "Bengaluru Central", "1 Market Road", "Bengaluru", "560001", 3),
            new("MYS-001", "Mysuru Central", "2 Lake Road", "Mysuru", "570001", 2),
            new("CHE-001", "Chennai T Nagar", "14 Usman Road", "Chennai", "600017", 3),
            new("HYD-001", "Hyderabad Central", "22 Abids Road", "Hyderabad", "500001", 3),
            new("PUN-001", "Pune Camp", "8 Moledina Road", "Pune", "411001", 2),
            new("MUM-001", "Mumbai Fort", "19 DN Road", "Mumbai", "400001", 4),
            new("DEL-001", "New Delhi Connaught Place", "12 Janpath", "New Delhi", "110001", 4),
            new("JAI-001", "Jaipur MI Road", "35 MI Road", "Jaipur", "302001", 2),
            new("BHO-001", "Bhopal MP Nagar", "Zone 2, MP Nagar", "Bhopal", "462011", 2),
            new("IDR-001", "Indore Vijay Nagar", "Scheme 54, Vijay Nagar", "Indore", "452010", 3),
            new("KOC-001", "Kochi Ernakulam", "6 MG Road", "Kochi", "682011", 2),
            new("AMD-001", "Ahmedabad Navrangpura", "21 CG Road", "Ahmedabad", "380009", 3),
            new("LKO-001", "Lucknow Hazratganj", "10 Hazratganj Road", "Lucknow", "226001", 2),
            new("KOL-001", "Kolkata Central", "17 Park Street", "Kolkata", "700016", 3),
            new("CHD-001", "Chandigarh Sector 17", "SCO 24, Sector 17", "Chandigarh", "160017", 2)
        ];

        public static readonly SeedBranch[] Branches =
            BranchInputs.Select((b, i) => new SeedBranch(
                StableId(1, i + 1),
                1,
                b.Code,
                b.Name,
                b.Address,
                null,
                b.City,
                b.PostalCode,
                "IN",
                b.CounterCount,
                BranchStatus.Active)).ToArray();

        private static readonly StaffInput[] StaffInputs =
        [
            new("EMP-0001", "Anika Sharma", 0, [StaffRole.BranchManager]),
            new("EMP-0002", "Rahul Verma", 1, [StaffRole.CounterClerk]),
            new("EMP-0003", "Priya Nair", 2, [StaffRole.BranchManager]),
            new("EMP-0004", "Arjun Reddy", 3, [StaffRole.CounterClerk]),
            new("EMP-0005", "Meera Iyer", 4, [StaffRole.BranchManager]),
            new("EMP-0006", "Kabir Deshmukh", 5, [StaffRole.CounterClerk]),
            new("EMP-0007", "Aditi Singh", 6, [StaffRole.BranchManager]),
            new("EMP-0008", "Vikram Joshi", 7, [StaffRole.CounterClerk]),
            new("EMP-0009", "Sneha Patel", 8, [StaffRole.BranchManager]),
            new("EMP-0010", "Rohan Kulkarni", 9, [StaffRole.CounterClerk]),
            new("EMP-0011", "Farah Thomas", 10, [StaffRole.BranchManager]),
            new("EMP-0012", "Dev Shah", 11, [StaffRole.CounterClerk]),
            new("EMP-0013", "Kavya Mishra", 12, [StaffRole.BranchManager]),
            new("EMP-0014", "Ishaan Das", 13, [StaffRole.CounterClerk]),
            new("EMP-0015", "Nisha Kaur", 14, [StaffRole.BranchManager]),
            new("EMP-0016", "Aditya Rao", 0, [StaffRole.CounterClerk]),
            new("EMP-0017", "Pooja Menon", 2, [StaffRole.CounterClerk]),
            new("EMP-0018", "Samar Khan", 5, [StaffRole.CounterClerk]),
            new("EMP-0019", "Neha Choudhary", 9, [StaffRole.CounterClerk]),
            new("EMP-0020", "Karan Banerjee", 13, [StaffRole.CounterClerk])
        ];

        public static readonly SeedStaff[] Staff =
            StaffInputs.Select((s, i) => new SeedStaff(
                StableId(2, i + 1),
                1,
                s.EmployeeNumber,
                s.Name,
                Branches[s.BranchIndex].Id,
                s.Roles,
                EmploymentStatus.Active)).ToArray();

        private static readonly ServiceInput[] ServiceInputs =
        [
            new("EXP-001", "Express Parcel", "Priority parcel delivery.", 125.50m),
            new("REG-001", "Registered Post", "Tracked standard delivery.", 45.00m),
            new("DOC-001", "Document Courier", "Tracked document envelope.", 65.00m),
            new("ECO-001", "Economy Parcel", "Economy parcel delivery.", 85.00m),
            new("SMD-001", "Same-Day Local", "Local same-day delivery where available.", 180.00m),
            new("INT-001", "Intercity Parcel", "Standard intercity parcel delivery.", 110.00m),
            new("FRG-001", "Fragile Handling", "Fragile-item handling service.", 150.00m),
            new("INS-001", "Insured Parcel", "Eligible declared-value cover.", 175.00m),
            new("RET-001", "Return Shipment", "Merchant return service.", 95.00m),
            new("BUL-001", "Bulk Consignment", "Multi-piece business consignment.", 250.00m),
            new("COD-001", "Cash-on-Delivery", "Eligible delivery collection service.", 70.00m),
            new("SIG-001", "Signature Required", "Delivery requiring recipient signature.", 35.00m),
            new("TRK-001", "Tracked Letter", "Letter service with tracking.", 40.00m),
            new("OVN-001", "Overnight Express", "Priority overnight delivery.", 220.00m),
            new("RUR-001", "Rural Delivery", "Delivery to supported rural destinations.", 100.00m),
            new("PKG-001", "Packaging Service", "Counter packaging assistance.", 25.00m),
            new("BOX-001", "Small Shipping Box", "Small standard shipping box.", 30.00m),
            new("BOX-002", "Medium Shipping Box", "Medium standard shipping box.", 50.00m),
            new("BOX-003", "Large Shipping Box", "Large standard shipping box.", 75.00m),
            new("ENV-001", "Document Envelope", "Padded document envelope.", 15.00m),
            new("TAP-001", "Packing Tape", "Packing tape roll.", 20.00m),
            new("WRP-001", "Protective Wrap", "Protective wrapping material.", 35.00m),
            new("LBL-001", "Printed Shipping Label", "Printed shipping label.", 5.00m),
            new("PRT-001", "Document Printing", "Black-and-white printing per page.", 3.00m),
            new("CPY-001", "Document Photocopy", "Photocopy service per page.", 2.00m),
            new("SCN-001", "Document Scanning", "Document scanning service.", 10.00m),
            new("POD-001", "Proof of Delivery", "Proof-of-delivery record service.", 20.00m),
            new("SMS-001", "SMS Tracking Alerts", "Shipment status notification add-on.", 12.00m),
            new("WGT-001", "Weight Recheck", "Counter weight verification.", 5.00m),
            new("HND-001", "Special Handling", "Eligible special-handling service.", 60.00m)
        ];

        public static readonly SeedService[] Services =
            ServiceInputs.Select((s, i) => new SeedService(
                StableId(3, i + 1),
                1,
                s.Code,
                s.Name,
                s.Description,
                ServiceAvailability.Available)).ToArray();

        public static readonly SeedPrice[] Prices =
            ServiceInputs.Select(s => new SeedPrice(
                s.Code,
                s.Price,
                "INR")).ToArray();
    }

    private sealed record SeedBranch(
        Guid Id,
        long Version,
        string Code,
        string Name,
        string Line1,
        string? Line2,
        string City,
        string PostalCode,
        string CountryCode,
        int CounterCount,
        BranchStatus Status);

    private sealed record SeedStaff(
        Guid Id,
        long Version,
        string EmployeeNumber,
        string Name,
        Guid BranchId,
        StaffRole[] Roles,
        EmploymentStatus Status);

    private sealed record SeedService(
        Guid Id,
        long Version,
        string Code,
        string Name,
        string Description,
        ServiceAvailability Availability);

    private sealed record SeedPrice(
        string ServiceCode,
        decimal Amount,
        string CurrencyCode);
}
