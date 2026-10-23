using ApexPost.Application.Results;

namespace ApexPost.Application.UseCases;

/// <summary>
/// Handles one application operation and accepts cancellation for any asynchronous work.
/// </summary>
public interface IUseCase<in TRequest, TResponse>
{
    Task<ApplicationResult<TResponse>> ExecuteAsync(TRequest request, CancellationToken cancellationToken);
}
