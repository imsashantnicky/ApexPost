using ApexPost.Application.Results;
using ApexPost.Domain.Common;
using ApexPost.Domain.Inventory;
using ApexPost.Application.Branches;
using ApexPost.Application.Security;

namespace ApexPost.Application.Inventory;

public sealed class InventoryOperationsUseCase
{
    private readonly IInventoryItemRepository repository;
    private readonly OperationalAccess access;
    public InventoryOperationsUseCase(IInventoryItemRepository repository, OperationalAccess access) { this.repository = repository ?? throw new ArgumentNullException(nameof(repository)); this.access = access; }

    public async Task<ApplicationResult<InventoryItemResponse>> GetAsync(Guid itemId, CancellationToken cancellationToken)
    {
        if (!TryId(itemId, out var id)) return Invalid<InventoryItemResponse>();
        var item = await repository.GetByIdAsync(id, cancellationToken);
        if (item is null) return NotFound<InventoryItemResponse>();
        var authorized = await access.CheckAsync(item.Value.BranchId.Value, OperationalCapability.Inventory, "Inventory.Get", cancellationToken);
        return authorized.IsSuccess ? ApplicationResults.Success(ToResponse(item.Value, item.Version)) : Failure<InventoryItemResponse>(authorized.Errors);
    }

    public async Task<ApplicationResult<IReadOnlyList<InventoryItemResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        var actorCheck = await access.CheckCurrentBranchAsync(OperationalCapability.Inventory, "Inventory.List", cancellationToken);
        if (!actorCheck.IsSuccess) return Failure<IReadOnlyList<InventoryItemResponse>>(actorCheck.Errors);
        var items = await repository.ListAsync(cancellationToken);
        var result = new List<InventoryItemResponse>();
        foreach (var item in items)
            if ((await access.CheckAsync(item.Value.BranchId.Value, OperationalCapability.Inventory, "Inventory.List", cancellationToken)).IsSuccess)
                result.Add(ToResponse(item.Value, item.Version));
        return ApplicationResults.Success<IReadOnlyList<InventoryItemResponse>>(result);
    }
    public async Task<ApplicationResult<InventoryItemResponse>> CreateAsync(CreateInventoryItemRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !TryId(request.BranchId, out var branchId)) return Invalid<InventoryItemResponse>();
        var authorized = await access.CheckAsync(branchId.Value, OperationalCapability.Inventory, "Inventory.Create", cancellationToken);
        if (!authorized.IsSuccess) return Failure<InventoryItemResponse>(authorized.Errors);
        try { var item = new InventoryItem(EntityId.New(), branchId, InventorySku.From(request.Sku), request.Name, request.ReorderLevel); await access.AuditAsync("Inventory.Create", "InventoryItem", item.Id.Value.ToString("D"), branchId.Value, "Started", null, cancellationToken); if (!await repository.TryAddAsync(item, cancellationToken)) { await access.AuditAsync("Inventory.Create", "InventoryItem", item.Id.Value.ToString("D"), branchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict<InventoryItemResponse>(); } await access.AuditAsync("Inventory.Create", "InventoryItem", item.Id.Value.ToString("D"), branchId.Value, "Succeeded", null, cancellationToken); return ApplicationResults.Success(ToResponse(item, 1)); }
        catch (DomainException exception) { await access.AuditAsync("Inventory.Create", "InventoryItem", branchId.Value.ToString("D"), branchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "domain.rule" }, cancellationToken); return DomainFailure<InventoryItemResponse>(exception); }
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
        var authorized = await access.CheckAsync(existing.Value.BranchId.Value, OperationalCapability.Inventory, "Inventory.Adjust", cancellationToken);
        if (!authorized.IsSuccess) return Failure<InventoryItemResponse>(authorized.Errors);
        if (existing.Version != request.ExpectedVersion) { await access.AuditAsync("Inventory.Adjust", "InventoryItem", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict<InventoryItemResponse>(); }
        try
        {
            operation(existing.Value, request);
            await access.AuditAsync("Inventory.Adjust", "InventoryItem", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Started", null, cancellationToken);
            if (!await repository.TryUpdateAsync(existing.Value, request.ExpectedVersion, cancellationToken)) { await access.AuditAsync("Inventory.Adjust", "InventoryItem", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "concurrency.conflict" }, cancellationToken); return Conflict<InventoryItemResponse>(); }
            await access.AuditAsync("Inventory.Adjust", "InventoryItem", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Succeeded", new Dictionary<string, string> { ["Quantity"] = request.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture), ["Version"] = (request.ExpectedVersion + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) }, cancellationToken);
            return ApplicationResults.Success(ToResponse(existing.Value, request.ExpectedVersion + 1));
        }
        catch (DomainException exception) { await access.AuditAsync("Inventory.Adjust", "InventoryItem", existing.Value.Id.Value.ToString("D"), existing.Value.BranchId.Value, "Failed", new Dictionary<string, string> { ["Status"] = "domain.rule" }, cancellationToken); return DomainFailure<InventoryItemResponse>(exception); }
    }

    private static InventoryItemResponse ToResponse(InventoryItem item, long version) => new(item.Id.Value, version, item.BranchId.Value, item.Sku.Value, item.Name, item.QuantityOnHand, item.ReorderLevel, item.NeedsReorder);
    private static bool TryId(Guid value, out EntityId id) { id = value == Guid.Empty ? null! : EntityId.From(value); return value != Guid.Empty; }
    private static ApplicationResult<T> Invalid<T>() => ApplicationResults.Failure<T>([new("request.invalid", "The request is invalid.")]);
    private static ApplicationResult<T> NotFound<T>() => ApplicationResults.Failure<T>([new("resource.not-found", "The requested resource was not found.")]);
    private static ApplicationResult<T> Conflict<T>() => ApplicationResults.Failure<T>([new("concurrency.conflict", "The resource changed before it could be saved. Reload and try again.")]);
    private static ApplicationResult<T> DomainFailure<T>(DomainException exception) => ApplicationResults.Failure<T>([new("domain.rule", exception.Message)]);
    private static ApplicationResult<T> Failure<T>(IReadOnlyList<ValidationError> errors) => ApplicationResults.Failure<T>(errors);
}
