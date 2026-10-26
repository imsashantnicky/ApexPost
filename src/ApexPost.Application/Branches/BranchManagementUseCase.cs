using ApexPost.Application.Results;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Common;
using ApexPost.Application.Security;

namespace ApexPost.Application.Branches;

public sealed class BranchManagementUseCase
{
    private readonly IBranchRepository repository;
    private readonly ICurrentActor currentActor;
    private readonly IAccessAuditWriter auditWriter;

    public BranchManagementUseCase(IBranchRepository repository, ICurrentActor currentActor, IAccessAuditWriter auditWriter)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.currentActor = currentActor ?? throw new ArgumentNullException(nameof(currentActor));
        this.auditWriter = auditWriter ?? throw new ArgumentNullException(nameof(auditWriter));
    }

    public async Task<ApplicationResult<BranchResponse>> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return InvalidRequest<BranchResponse>();
        }

        try
        {
            var branch = new Branch(EntityId.New(), BranchCode.From(request.Code), request.Name,
                new BranchAddress(request.AddressLine1, request.AddressLine2, request.City, request.PostalCode, request.CountryCode), request.CounterCount);
            await AuditAsync("Branch.Create", branch, "Started", cancellationToken);
            if (!await repository.TryAddAsync(branch, cancellationToken))
            {
                await AuditAsync("Branch.Create", branch, "Failed", cancellationToken);
                return Duplicate<BranchResponse>("code");
            }

            await AuditAsync("Branch.Create", branch, "Succeeded", cancellationToken);

            return ApplicationResults.Success(ToResponse(branch, 1));
        }
        catch (DomainException exception)
        {
            return DomainFailure<BranchResponse>(exception);
        }
    }

    public async Task<ApplicationResult<BranchResponse>> GetAsync(Guid branchId, CancellationToken cancellationToken)
    {
        if (!TryId(branchId, out var id)) return InvalidId<BranchResponse>("branchId");
        var result = await repository.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound<BranchResponse>("branchId") : ApplicationResults.Success(ToResponse(result.Value, result.Version));
    }

    public async Task<IReadOnlyList<BranchResponse>> ListAsync(CancellationToken cancellationToken)
    {
        return (await repository.ListAsync(cancellationToken)).Select(item => ToResponse(item.Value, item.Version)).OrderBy(item => item.Code).ToArray();
    }

    public async Task<ApplicationResult<BranchResponse>> UpdateAsync(UpdateBranchRequest request, CancellationToken cancellationToken)
    {
        if (request is null) return InvalidRequest<BranchResponse>();
        if (!TryId(request.BranchId, out var id) || request.ExpectedVersion < 1) return InvalidRequest<BranchResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<BranchResponse>("branchId");
        if (existing.Version != request.ExpectedVersion) { await AuditAsync("Branch.Update", existing.Value, "Failed", cancellationToken); return Conflict<BranchResponse>(); }
        try
        {
            existing.Value.Rename(request.Name);
            existing.Value.ChangeAddress(new BranchAddress(request.AddressLine1, request.AddressLine2, request.City, request.PostalCode, request.CountryCode));
            existing.Value.SetCounterCount(request.CounterCount);
            await AuditAsync("Branch.Update", existing.Value, "Started", cancellationToken);
            if (!await repository.TryUpdateAsync(existing.Value, request.ExpectedVersion, cancellationToken)) return Conflict<BranchResponse>();
            await AuditAsync("Branch.Update", existing.Value, "Succeeded", cancellationToken);
            return ApplicationResults.Success(ToResponse(existing.Value, request.ExpectedVersion + 1));
        }
        catch (DomainException exception) { return DomainFailure<BranchResponse>(exception); }
    }

    public async Task<ApplicationResult<BranchResponse>> DeactivateAsync(Guid branchId, long expectedVersion, CancellationToken cancellationToken)
    {
        if (!TryId(branchId, out var id) || expectedVersion < 1) return InvalidRequest<BranchResponse>();
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null) return NotFound<BranchResponse>("branchId");
        if (existing.Version != expectedVersion) { await AuditAsync("Branch.Deactivate", existing.Value, "Failed", cancellationToken); return Conflict<BranchResponse>(); }
        try
        {
            existing.Value.Suspend();
            await AuditAsync("Branch.Deactivate", existing.Value, "Started", cancellationToken);
            if (!await repository.TryUpdateAsync(existing.Value, expectedVersion, cancellationToken)) return Conflict<BranchResponse>();
            await AuditAsync("Branch.Deactivate", existing.Value, "Succeeded", cancellationToken);
            return ApplicationResults.Success(ToResponse(existing.Value, expectedVersion + 1));
        }
        catch (DomainException exception) { return DomainFailure<BranchResponse>(exception); }
    }

    private static BranchResponse ToResponse(Branch branch, long version) => new(branch.Id.Value, version, branch.Code.Value, branch.Name, branch.Address.Line1, branch.Address.Line2, branch.Address.City, branch.Address.PostalCode, branch.Address.CountryCode, branch.CounterCount, branch.Status);
    private Task AuditAsync(string operation, Branch branch, string outcome, CancellationToken token) => auditWriter.WriteOperationAsync(currentActor.Actor, branch.Id.Value, operation, "Branch", branch.Id.Value.ToString("D"), outcome, new Dictionary<string, string> { ["Status"] = branch.Status.ToString(), ["CounterCount"] = branch.CounterCount.ToString(System.Globalization.CultureInfo.InvariantCulture) }, token);
    private static bool TryId(Guid value, out EntityId id) { id = value == Guid.Empty ? null! : EntityId.From(value); return value != Guid.Empty; }
    private static ApplicationResult<T> InvalidRequest<T>() => ApplicationResults.Failure<T>([new("request.invalid", "The request is invalid.")]);
    private static ApplicationResult<T> InvalidId<T>(string field) => ApplicationResults.Failure<T>([new("identifier.invalid", "A valid identifier is required.", field)]);
    private static ApplicationResult<T> NotFound<T>(string field) => ApplicationResults.Failure<T>([new("resource.not-found", "The requested resource was not found.", field)]);
    private static ApplicationResult<T> Duplicate<T>(string field) => ApplicationResults.Failure<T>([new("identifier.duplicate", "The business identifier is already in use.", field)]);
    private static ApplicationResult<T> Conflict<T>() => ApplicationResults.Failure<T>([new("concurrency.conflict", "The resource changed before it could be saved. Reload and try again.")]);
    private static ApplicationResult<T> DomainFailure<T>(DomainException exception) => ApplicationResults.Failure<T>([new("domain.rule", exception.Message)]);
}
