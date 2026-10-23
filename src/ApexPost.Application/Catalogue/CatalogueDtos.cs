using ApexPost.Domain.Catalogue;

namespace ApexPost.Application.Catalogue;

public sealed record CreateServiceRequest(string Code, string Name, string Description);
public sealed record UpdateServiceRequest(Guid ServiceId, long ExpectedVersion, string Name, string Description);
public sealed record ServiceResponse(Guid Id, long Version, string Code, string Name, string Description, ServiceAvailability Availability);
