using ApexPost.Application.Results;
using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;
using ApexPost.Application.Transactions;
using ApexPost.Application.Security;

namespace ApexPost.Application.Shipping;

public sealed class ShipmentOperationsUseCase
{
    private readonly IShipmentRepository repository;
    private readonly IServicePriceLookup prices;
    private readonly IShippingRateLookup rates;
    private readonly OperationalAccess access;
    private readonly ShipmentPolicy policy;
    public ShipmentOperationsUseCase(IShipmentRepository repository, IServicePriceLookup prices, IShippingRateLookup rates, OperationalAccess access, ShipmentPolicy policy)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.prices = prices ?? throw new ArgumentNullException(nameof(prices));
        this.rates = rates ?? throw new ArgumentNullException(nameof(rates));
        this.access = access;
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async Task<ApplicationResult<ShipmentResponse>> BookAsync(BookShipmentRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.ServiceId, out var serviceId)) return Invalid<ShipmentResponse>();
        var authorized = await access.CheckAsync(request.BranchId, OperationalCapability.Counter, "Shipment.Book", cancellationToken);
        if (!authorized.IsSuccess) return Failure<ShipmentResponse>(authorized.Errors);
        try
        {
            var price = await prices.GetCurrentPriceAsync(serviceId, request.CurrencyCode, cancellationToken);
            if (price is null) return ApplicationResults.Failure<ShipmentResponse>([new("service.unavailable", "The selected service is unavailable or has no current price.", "serviceId")]);
            var currentRate = await rates.GetCurrentRateAsync(request.ZoneCode, request.ServiceLevel, request.CurrencyCode, cancellationToken);
            if (currentRate is null) return ApplicationResults.Failure<ShipmentResponse>([new("shipping.rate.unavailable", "The selected zone and service level have no current shipping rate.", "zoneCode")]);
            if (currentRate.Value != request.ChargeAmount) return ApplicationResults.Failure<ShipmentResponse>([new("charge.invalid", "Shipment charge must match the current zone and service-level rate.", "chargeAmount")]);
            var charge = new ShipmentCharge(currentRate.Value, request.CurrencyCode);
            var shipment = new Shipment(EntityId.New(), ShipmentNumber.From(request.ShipmentNumber),
                Party(request.SenderName, request.SenderAddressLine1, request.SenderCity, request.SenderPostalCode, request.SenderCountryCode),
                Party(request.RecipientName, request.RecipientAddressLine1, request.RecipientCity, request.RecipientPostalCode, request.RecipientCountryCode),
                serviceId, new ShipmentPackage(request.WeightKilograms, request.ContentsDescription, policy.MaximumWeightKilograms), charge,
                EntityId.From(request.BranchId), request.ZoneCode, request.ServiceLevel);
            await access.AuditAsync("Shipment.Book", "Shipment", shipment.Id.Value.ToString("D"), request.BranchId, "Started", null, cancellationToken);
            if (!await repository.TryAddAsync(shipment, cancellationToken)) { await access.AuditAsync("Shipment.Book", "Shipment", shipment.Id.Value.ToString("D"), request.BranchId, "Failed", new Dictionary<string, string> { ["Status"] = "identifier.duplicate" }, cancellationToken); return Duplicate<ShipmentResponse>("shipmentNumber"); }
            await access.AuditAsync("Shipment.Book", "Shipment", shipment.Id.Value.ToString("D"), shipment.BranchId?.Value, "Succeeded", null, cancellationToken);
            return ApplicationResults.Success(ToResponse(shipment, 1));
        }
        catch (DomainException exception) { await access.AuditAsync("Shipment.Book", "Shipment", request.BranchId.ToString("D"), request.BranchId, "Failed", new Dictionary<string, string> { ["Status"] = "domain.rule" }, cancellationToken); return DomainFailure<ShipmentResponse>(exception); }
    }

    public async Task<ApplicationResult<ShipmentResponse>> GetAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        if (!TryId(shipmentId, out var id)) return Invalid<ShipmentResponse>();
        var shipment = await repository.GetByIdAsync(id, cancellationToken);
        if (shipment is null) return NotFound<ShipmentResponse>();
        if (shipment.Value.BranchId is null) return NotFound<ShipmentResponse>();
        var authorized = await access.CheckAsync(shipment.Value.BranchId.Value, OperationalCapability.Counter, "Shipment.Get", cancellationToken, requireOpenBranch: false);
        return authorized.IsSuccess ? ApplicationResults.Success(ToResponse(shipment.Value, shipment.Version)) : Failure<ShipmentResponse>(authorized.Errors);
    }

    public async Task<ApplicationResult<IReadOnlyList<ShipmentResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        var actorCheck = await access.CheckCurrentBranchAsync(OperationalCapability.Counter, "Shipment.List", cancellationToken);
        if (!actorCheck.IsSuccess) return Failure<IReadOnlyList<ShipmentResponse>>(actorCheck.Errors);
        var result = new List<ShipmentResponse>();
        foreach (var item in await repository.ListAsync(cancellationToken))
            if (item.Value.BranchId is not null && (await access.CheckAsync(item.Value.BranchId.Value, OperationalCapability.Counter, "Shipment.List", cancellationToken, requireOpenBranch: false)).IsSuccess)
                result.Add(ToResponse(item.Value, item.Version));
        return ApplicationResults.Success<IReadOnlyList<ShipmentResponse>>(result);
    }

    public Task<ApplicationResult<ShipmentResponse>> AcceptAsync(Guid id, long version, CancellationToken token) => ChangeAsync(id, version, static shipment => shipment.Accept(), token);
    public Task<ApplicationResult<ShipmentResponse>> DispatchAsync(Guid id, long version, CancellationToken token) => ChangeAsync(id, version, static shipment => shipment.Dispatch(), token);
    public Task<ApplicationResult<ShipmentResponse>> DeliverAsync(Guid id, long version, CancellationToken token) => ChangeAsync(id, version, static shipment => shipment.Deliver(), token);
    public Task<ApplicationResult<ShipmentResponse>> CancelAsync(Guid id, long version, CancellationToken token) => ChangeAsync(id, version, static shipment => shipment.Cancel(), token);

    private async Task<ApplicationResult<ShipmentResponse>> ChangeAsync(Guid shipmentId, long expectedVersion, Action<Shipment> operation, CancellationToken cancellationToken)
    {
        if (!TryId(shipmentId, out var id) || expectedVersion < 1) return Invalid<ShipmentResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<ShipmentResponse>();
        if (existing.Value.BranchId is null) return NotFound<ShipmentResponse>();
        var authorized = await access.CheckAsync(existing.Value.BranchId.Value, OperationalCapability.Counter, "Shipment.Change", cancellationToken);
        if (!authorized.IsSuccess) return Failure<ShipmentResponse>(authorized.Errors);
        if (existing.Version != expectedVersion) { await access.AuditAsync("Shipment.Change", "Shipment", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict<ShipmentResponse>(); }
        try
        {
            operation(existing.Value);
            await access.AuditAsync("Shipment.Change", "Shipment", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Started", null, cancellationToken);
            if (!await repository.TryUpdateAsync(existing.Value, expectedVersion, cancellationToken)) { await access.AuditAsync("Shipment.Change", "Shipment", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict<ShipmentResponse>(); }
            await access.AuditAsync("Shipment.Change", "Shipment", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId?.Value, "Succeeded", new Dictionary<string, string> { ["Status"] = existing.Value.Status.ToString(), ["Version"] = (expectedVersion + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) }, cancellationToken);
            return ApplicationResults.Success(ToResponse(existing.Value, expectedVersion + 1));
        }
        catch (DomainException exception) { await access.AuditAsync("Shipment.Change", "Shipment", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "domain.rule" }, cancellationToken); return DomainFailure<ShipmentResponse>(exception); }
    }

    private static ShipmentParty Party(string name, string line1, string city, string postalCode, string countryCode) => new(name, new ShipmentAddress(line1, city, postalCode, countryCode));
    private static ShipmentResponse ToResponse(Shipment shipment, long version) => new(shipment.Id.Value, version, shipment.BranchId?.Value ?? Guid.Empty, shipment.ShipmentNumber.Value, shipment.Status, shipment.Charge.Amount, shipment.Charge.CurrencyCode)
    {
        ZoneCode = shipment.ZoneCode,
        ServiceLevel = shipment.ServiceLevel
    };
    private static bool TryId(Guid value, out EntityId id) { id = value == Guid.Empty ? null! : EntityId.From(value); return value != Guid.Empty; }
    private static ApplicationResult<T> Invalid<T>() => ApplicationResults.Failure<T>([new("request.invalid", "The request is invalid.")]);
    private static ApplicationResult<T> NotFound<T>() => ApplicationResults.Failure<T>([new("resource.not-found", "The requested resource was not found.")]);
    private static ApplicationResult<T> Duplicate<T>(string field) => ApplicationResults.Failure<T>([new("identifier.duplicate", "The business identifier is already in use.", field)]);
    private static ApplicationResult<T> Conflict<T>() => ApplicationResults.Failure<T>([new("concurrency.conflict", "The resource changed before it could be saved. Reload and try again.")]);
    private static ApplicationResult<T> DomainFailure<T>(DomainException exception) => ApplicationResults.Failure<T>([new("domain.rule", exception.Message)]);
    private static ApplicationResult<T> Failure<T>(IReadOnlyList<ValidationError> errors) => ApplicationResults.Failure<T>(errors);
}

public sealed record ShipmentPolicy
{
    public ShipmentPolicy(decimal maximumWeightKilograms)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumWeightKilograms, 0m);
        MaximumWeightKilograms = maximumWeightKilograms;
    }

    public decimal MaximumWeightKilograms { get; }
}
