using ApexPost.Application.Results;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;

namespace ApexPost.Application.Catalogue;

public sealed class CatalogueManagementUseCase
{
    private readonly IServiceDefinitionRepository repository;
    public CatalogueManagementUseCase(IServiceDefinitionRepository repository) => this.repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<ApplicationResult<ServiceResponse>> CreateAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        if (request is null) return Invalid<ServiceResponse>();
        try
        {
            var service = new ServiceDefinition(EntityId.New(), ServiceCode.From(request.Code), request.Name, request.Description);
            if (!await repository.TryAddAsync(service, cancellationToken)) return Duplicate<ServiceResponse>("code");
            return ApplicationResults.Success(ToResponse(service, 1));
        }
        catch (DomainException exception) { return DomainFailure<ServiceResponse>(exception); }
    }

    public async Task<ApplicationResult<ServiceResponse>> GetAsync(Guid serviceId, CancellationToken cancellationToken)
    {
        if (!TryId(serviceId, out var id)) return Invalid<ServiceResponse>();
        var result = await repository.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound<ServiceResponse>() : ApplicationResults.Success(ToResponse(result.Value, result.Version));
    }

    public async Task<ApplicationResult<ServiceResponse>> UpdateAsync(UpdateServiceRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.ServiceId, out var id) || request.ExpectedVersion < 1) return Invalid<ServiceResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<ServiceResponse>();
        if (existing.Version != request.ExpectedVersion) return Conflict<ServiceResponse>();
        try
        {
            existing.Value.UpdateDetails(request.Name, request.Description);
            return await repository.TryUpdateAsync(existing.Value, request.ExpectedVersion, cancellationToken)
                ? ApplicationResults.Success(ToResponse(existing.Value, request.ExpectedVersion + 1)) : Conflict<ServiceResponse>();
        }
        catch (DomainException exception) { return DomainFailure<ServiceResponse>(exception); }
    }

    public async Task<ApplicationResult<ServiceResponse>> DeactivateAsync(Guid serviceId, long expectedVersion, CancellationToken cancellationToken)
    {
        if (!TryId(serviceId, out var id) || expectedVersion < 1) return Invalid<ServiceResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<ServiceResponse>();
        if (existing.Version != expectedVersion) return Conflict<ServiceResponse>();
        try
        {
            existing.Value.MakeUnavailable();
            return await repository.TryUpdateAsync(existing.Value, expectedVersion, cancellationToken)
                ? ApplicationResults.Success(ToResponse(existing.Value, expectedVersion + 1)) : Conflict<ServiceResponse>();
        }
        catch (DomainException exception) { return DomainFailure<ServiceResponse>(exception); }
    }

    private static ServiceResponse ToResponse(ServiceDefinition service, long version) => new(service.Id.Value, version, service.Code.Value, service.Name, service.Description, service.Availability);
    private static bool TryId(Guid value, out EntityId id) { id = value == Guid.Empty ? null! : EntityId.From(value); return value != Guid.Empty; }
    private static ApplicationResult<T> Invalid<T>() => ApplicationResults.Failure<T>([new("request.invalid", "The request is invalid.")]);
    private static ApplicationResult<T> NotFound<T>() => ApplicationResults.Failure<T>([new("resource.not-found", "The requested resource was not found.")]);
    private static ApplicationResult<T> Duplicate<T>(string field) => ApplicationResults.Failure<T>([new("identifier.duplicate", "The business identifier is already in use.", field)]);
    private static ApplicationResult<T> Conflict<T>() => ApplicationResults.Failure<T>([new("concurrency.conflict", "The resource changed before it could be saved. Reload and try again.")]);
    private static ApplicationResult<T> DomainFailure<T>(DomainException exception) => ApplicationResults.Failure<T>([new("domain.rule", exception.Message)]);
}
