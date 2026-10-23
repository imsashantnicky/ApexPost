using ApexPost.Application.Results;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;

namespace ApexPost.Application.Inventory;

public sealed class InventoryOperationsUseCase
{
    private readonly IInventoryItemRepository repository;
    public InventoryOperationsUseCase(IInventoryItemRepository repository) => this.repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<ApplicationResult<InventoryItemResponse>> GetAsync(Guid itemId, CancellationToken cancellationToken)
    {
        if (!TryId(itemId, out var id)) return Invalid<InventoryItemResponse>();
        var item = await repository.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound<InventoryItemResponse>() : ApplicationResults.Success(ToResponse(item.Value, item.Version));
    }

    public async Task<ApplicationResult<InventoryItemResponse>> ReceiveAsync(AdjustInventoryRequest request, CancellationToken cancellationToken)
    {
        return await ChangeAsync(request, static (item, command) => item.Receive(command.Quantity, command.Reason, command.OccurredAt), cancellationToken);
    }

    public async Task<ApplicationResult<InventoryItemResponse>> IssueAsync(AdjustInventoryRequest request, CancellationToken cancellationToken)
    {
        return await ChangeAsync(request, static (item, command) => item.Issue(command.Quantity, command.Reason, command.OccurredAt), cancellationToken);
    }

    public async Task<ApplicationResult<InventoryItemResponse>> AdjustToAsync(AdjustInventoryRequest request, CancellationToken cancellationToken)
    {
        return await ChangeAsync(request, static (item, command) => item.AdjustTo(command.Quantity, command.Reason, command.OccurredAt), cancellationToken);
    }

    private async Task<ApplicationResult<InventoryItemResponse>> ChangeAsync(AdjustInventoryRequest request, Action<InventoryItem, AdjustInventoryRequest> operation, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.InventoryItemId, out var id) || request.ExpectedVersion < 1) return Invalid<InventoryItemResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<InventoryItemResponse>();
        if (existing.Version != request.ExpectedVersion) return Conflict<InventoryItemResponse>();
        try
        {
            operation(existing.Value, request);
            return await repository.TryUpdateAsync(existing.Value, request.ExpectedVersion, cancellationToken)
                ? ApplicationResults.Success(ToResponse(existing.Value, request.ExpectedVersion + 1)) : Conflict<InventoryItemResponse>();
        }
        catch (DomainException exception) { return DomainFailure<InventoryItemResponse>(exception); }
    }

    private static InventoryItemResponse ToResponse(InventoryItem item, long version) => new(item.Id.Value, version, item.BranchId.Value, item.Sku.Value, item.Name, item.QuantityOnHand, item.ReorderLevel, item.NeedsReorder);
    private static bool TryId(Guid value, out EntityId id) { id = value == Guid.Empty ? null! : EntityId.From(value); return value != Guid.Empty; }
    private static ApplicationResult<T> Invalid<T>() => ApplicationResults.Failure<T>([new("request.invalid", "The request is invalid.")]);
    private static ApplicationResult<T> NotFound<T>() => ApplicationResults.Failure<T>([new("resource.not-found", "The requested resource was not found.")]);
    private static ApplicationResult<T> Conflict<T>() => ApplicationResults.Failure<T>([new("concurrency.conflict", "The resource changed before it could be saved. Reload and try again.")]);
    private static ApplicationResult<T> DomainFailure<T>(DomainException exception) => ApplicationResults.Failure<T>([new("domain.rule", exception.Message)]);
}
