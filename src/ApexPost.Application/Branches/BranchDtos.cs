using ApexPost.Domain.Branches;

namespace ApexPost.Application.Branches;

public sealed record CreateBranchRequest(string Code, string Name, string AddressLine1, string? AddressLine2, string City, string PostalCode, string CountryCode, int CounterCount);
public sealed record UpdateBranchRequest(Guid BranchId, long ExpectedVersion, string Name, string AddressLine1, string? AddressLine2, string City, string PostalCode, string CountryCode, int CounterCount);
public sealed record BranchResponse(Guid Id, long Version, string Code, string Name, string AddressLine1, string? AddressLine2, string City, string PostalCode, string CountryCode, int CounterCount, BranchStatus Status);
