
using System.Text.Json;
using ApexPost.Domain.Common;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.IdentityVerification;
using ApexPost.Domain.Inventory;
using ApexPost.Domain.MoneyOrders;
using ApexPost.Domain.Payments;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Staff;
using ApexPost.Domain.Transactions;

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

        // Validate and preserve existing records, then fill absent seed keys.
        // accounts.json is deliberately excluded from business seeding.
        var existingBusinessFiles = Directory
            .EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(file => !string.Equals(
                Path.GetFileName(file),
                "accounts.json",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (existingBusinessFiles.Length > 0)
            await ValidateExistingFilesAsync(existingBusinessFiles, cancellationToken);

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

        await SeedOperationalDataAsync(directory, cancellationToken);
    }

    private static async Task SeedOperationalDataAsync(string directory, CancellationToken token)
    {
        var options = new JsonDataOptions { DataDirectory = directory };
        var rates = new JsonCollectionStore<SeedShippingRate>(directory, "shipping-rates.json");
        foreach (var rate in SeedData.ShippingRates)
            await rates.TryAddAsync($"{rate.ZoneCode}|{rate.ServiceLevel}|{rate.CurrencyCode}", rate, token);

        var inventory = new JsonInventoryItemRepository(options);
        for (var index = 0; index < 30; index++)
        {
            token.ThrowIfCancellationRequested();
            var occurredAt = SeedData.SampleDate(index);
            var item = new InventoryItem(EntityId.From(SeedData.StableId(6, index + 1)),
                EntityId.From(SeedData.Branches[index % SeedData.Branches.Length].Id),
                InventorySku.From($"ENV-{index + 1:000}"), $"Synthetic stock item {index + 1:00}", 3);
            item.Receive(index % 4 == 0 ? 2 : 10, "Synthetic opening stock", occurredAt);
            if (index % 2 == 1) item.Issue(4, "Synthetic counter use", occurredAt.AddHours(1));
            await inventory.TryAddAsync(item, token);
        }

        var shipments = new JsonShipmentRepository(options);
        for (var index = 0; index < 20; index++)
        {
            token.ThrowIfCancellationRequested();
            var shipment = Shipment.Restore(EntityId.From(SeedData.StableId(7, index + 1)),
                ShipmentNumber.From($"HS-{index + 1:000000}"),
                new ShipmentParty("Synthetic Sender", new ShipmentAddress("1 Sample Road", "Pune", "411001", "IN")),
                new ShipmentParty("Synthetic Recipient", new ShipmentAddress("2 Sample Road", "Mumbai", "400001", "IN")),
                EntityId.From(SeedData.Services[index % SeedData.Services.Length].Id),
                new ShipmentPackage(1m + index % 5, "Synthetic sample goods"),
                new ShipmentCharge(75m + index, "INR"), (ShipmentStatus)(index % 5),
                EntityId.From(SeedData.Branches[index % SeedData.Branches.Length].Id),
                $"Z{index % 10 + 1:00}", (ShippingServiceLevel)(index % 3));
            await shipments.TryAddAsync(shipment, token);
        }

        var verifications = new JsonIdentityVerificationRepository(options);
        var verificationIds = new Guid[12];
        for (var index = 0; index < verificationIds.Length; index++)
        {
            token.ThrowIfCancellationRequested();
            var status = (index % 3) switch
            {
                0 => VerificationStatus.Pending,
                1 => VerificationStatus.Verified,
                _ => VerificationStatus.Rejected
            };
            DateTimeOffset? completedAt = status == VerificationStatus.Pending ? null : SeedData.SampleDate(index);
            var rejection = status == VerificationStatus.Rejected ? "Synthetic sample review rejection" : null;
            var record = IdentityVerificationRecord.Restore(EntityId.From(SeedData.StableId(8, index + 1)),
                EntityId.From(SeedData.StableId(10, index + 1)), VerificationMethod.InPersonReview,
                EntityId.From(SeedData.Branches[index % SeedData.Branches.Length].Id), status, completedAt, rejection);
            verificationIds[index] = record.Id.Value;
            await verifications.TryAddAsync(record, token);
        }

        var moneyOrders = new JsonMoneyOrderRepository(options);
        for (var index = 0; index < 16; index++)
        {
            token.ThrowIfCancellationRequested();
            var verificationId = index % 3 == 1 ? verificationIds[index % verificationIds.Length] : (Guid?)null;
            var amount = verificationId is null ? 150m + index * 10m : 600m + index * 10m;
            var order = MoneyOrder.Restore(EntityId.From(SeedData.StableId(9, index + 1)),
                MoneyOrderNumber.From($"MO-{index + 1:000000}"), new MoneyOrderAmount(amount, "INR"),
                EntityId.From(SeedData.Branches[index % SeedData.Branches.Length].Id),
                new MoneyOrderBeneficiary($"Synthetic Beneficiary {index + 1:00}", $"SAMPLE-REF-{index + 1:0000}"),
                (MoneyOrderStatus)(index % 4), verificationId is Guid id ? EntityId.From(id) : null);
            await moneyOrders.TryAddAsync(order, token);
        }

        var transactions = new JsonRetailTransactionRepository(options);
        for (var index = 0; index < 40; index++)
        {
            token.ThrowIfCancellationRequested();
            var transaction = new RetailTransaction(EntityId.From(SeedData.StableId(4, index + 1)),
                EntityId.From(SeedData.Branches[index % SeedData.Branches.Length].Id),
                $"HIST-{index + 1:0000}", "INR", SeedData.SampleDate(index));
            transaction.AddLineItem(EntityId.From(SeedData.Services[(index + 16) % SeedData.Services.Length].Id),
                "Synthetic retail catalogue item", 1, 30m + index % 10, TransactionLineKind.RetailProduct);
            transaction.AddLineItem(EntityId.From(SeedData.Services[index % SeedData.Services.Length].Id),
                "Synthetic counter service", 1, 40m + index % 7, TransactionLineKind.Service);
            switch (index % 5)
            {
                case 1:
                    transaction.StartPayment(transaction.Total, PaymentMethod.Cash, $"SAMPLE-PAY-{index + 1:0000}");
                    break;
                case 2:
                    transaction.StartPayment(transaction.Total, PaymentMethod.Cash, $"SAMPLE-PAY-{index + 1:0000}");
                    transaction.ConfirmPayment();
                    break;
                case 3:
                    transaction.Cancel("Synthetic sample cancellation");
                    break;
                case 4:
                    transaction.StartPayment(transaction.Total, PaymentMethod.Cash, $"SAMPLE-PAY-{index + 1:0000}");
                    transaction.ConfirmPayment();
                    transaction.ReversePayment("Synthetic sample reversal");
                    break;
            }
            await transactions.TryAddAsync(transaction, token);
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
            StaffRole[] Roles,
            EmploymentStatus Status = EmploymentStatus.Active);

        private sealed record ServiceInput(
            string Code,
            string Name,
            string Description,
            decimal Price);

        public static Guid StableId(int group, int number) =>
            Guid.Parse(
                $"{group:D8}-0000-0000-0000-{number:D12}");

        public static DateTimeOffset SampleDate(int index) =>
            new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).AddDays(-(index % 40)).AddHours(8 + index % 9);

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
            new("EMP-0016", "Aditya Rao", 0, [StaffRole.CounterClerk, StaffRole.InventoryClerk]),
            new("EMP-0017", "Pooja Menon", 2, [StaffRole.CounterClerk]),
            new("EMP-0018", "Samar Khan", 5, [StaffRole.CounterClerk]),
            new("EMP-0019", "Neha Choudhary", 9, [StaffRole.InventoryClerk]),
            new("EMP-0020", "Karan Banerjee", 13, [StaffRole.CounterClerk], EmploymentStatus.Suspended)
        ];

        public static readonly SeedStaff[] Staff =
            StaffInputs.Select((s, i) => new SeedStaff(
                StableId(2, i + 1),
                1,
                s.EmployeeNumber,
                s.Name,
                Branches[s.BranchIndex].Id,
                s.Roles,
                s.Status)).ToArray();

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

        public static readonly SeedShippingRate[] ShippingRates = Enumerable.Range(1, 10)
            .SelectMany(zone => Enum.GetValues<ShippingServiceLevel>().Select(level =>
                new SeedShippingRate($"Z{zone:00}", level, 75m + zone * 5m + (int)level * 20m, "INR")))
            .ToArray();
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

    private sealed record SeedShippingRate(string ZoneCode, ShippingServiceLevel ServiceLevel, decimal Amount, string CurrencyCode);
}
