using ApexPost.Application.Persistence;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;

namespace ApexPost.Application.Catalogue;

public interface IServiceDefinitionRepository
{
    Task<IReadOnlyList<Versioned<ServiceDefinition>>> ListAsync(CancellationToken cancellationToken);
    Task<Versioned<ServiceDefinition>?> GetByIdAsync(EntityId id, CancellationToken cancellationToken);
    Task<bool> TryAddAsync(ServiceDefinition serviceDefinition, CancellationToken cancellationToken);
    Task<bool> TryUpdateAsync(ServiceDefinition serviceDefinition, long expectedVersion, CancellationToken cancellationToken);
}
