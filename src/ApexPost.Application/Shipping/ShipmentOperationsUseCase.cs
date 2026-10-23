using ApexPost.Application.Results;
using ApexPost.Domain.Common;
using ApexPost.Domain.Shipping;

namespace ApexPost.Application.Shipping;

public sealed class ShipmentOperationsUseCase
{
    private readonly IShipmentRepository repository;
    public ShipmentOperationsUseCase(IShipmentRepository repository) => this.repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<ApplicationResult<ShipmentResponse>> BookAsync(BookShipmentRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.ServiceId, out var serviceId)) return Invalid<ShipmentResponse>();
        try
        {
            var shipment = new Shipment(EntityId.New(), ShipmentNumber.From(request.ShipmentNumber),
                Party(request.SenderName, request.SenderAddressLine1, request.SenderCity, request.SenderPostalCode, request.SenderCountryCode),
                Party(request.RecipientName, request.RecipientAddressLine1, request.RecipientCity, request.RecipientPostalCode, request.RecipientCountryCode),
                serviceId, new ShipmentPackage(request.WeightKilograms, request.ContentsDescription), new ShipmentCharge(request.ChargeAmount, request.CurrencyCode));
            if (!await repository.TryAddAsync(shipment, cancellationToken)) return Duplicate<ShipmentResponse>("shipmentNumber");
            return ApplicationResults.Success(ToResponse(shipment, 1));
        }
        catch (DomainException exception) { return DomainFailure<ShipmentResponse>(exception); }
    }

    public async Task<ApplicationResult<ShipmentResponse>> GetAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        if (!TryId(shipmentId, out var id)) return Invalid<ShipmentResponse>();
        var shipment = await repository.GetByIdAsync(id, cancellationToken);
        return shipment is null ? NotFound<ShipmentResponse>() : ApplicationResults.Success(ToResponse(shipment.Value, shipment.Version));
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
        if (existing.Version != expectedVersion) return Conflict<ShipmentResponse>();
        try
        {
            operation(existing.Value);
            return await repository.TryUpdateAsync(existing.Value, expectedVersion, cancellationToken)
                ? ApplicationResults.Success(ToResponse(existing.Value, expectedVersion + 1)) : Conflict<ShipmentResponse>();
        }
        catch (DomainException exception) { return DomainFailure<ShipmentResponse>(exception); }
    }

    private static ShipmentParty Party(string name, string line1, string city, string postalCode, string countryCode) => new(name, new ShipmentAddress(line1, city, postalCode, countryCode));
    private static ShipmentResponse ToResponse(Shipment shipment, long version) => new(shipment.Id.Value, version, shipment.ShipmentNumber.Value, shipment.Status, shipment.Charge.Amount, shipment.Charge.CurrencyCode);
    private static bool TryId(Guid value, out EntityId id) { id = value == Guid.Empty ? null! : EntityId.From(value); return value != Guid.Empty; }
    private static ApplicationResult<T> Invalid<T>() => ApplicationResults.Failure<T>([new("request.invalid", "The request is invalid.")]);
    private static ApplicationResult<T> NotFound<T>() => ApplicationResults.Failure<T>([new("resource.not-found", "The requested resource was not found.")]);
    private static ApplicationResult<T> Duplicate<T>(string field) => ApplicationResults.Failure<T>([new("identifier.duplicate", "The business identifier is already in use.", field)]);
    private static ApplicationResult<T> Conflict<T>() => ApplicationResults.Failure<T>([new("concurrency.conflict", "The resource changed before it could be saved. Reload and try again.")]);
    private static ApplicationResult<T> DomainFailure<T>(DomainException exception) => ApplicationResults.Failure<T>([new("domain.rule", exception.Message)]);
}
