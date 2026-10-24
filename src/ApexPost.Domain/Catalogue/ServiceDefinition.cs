using ApexPost.Domain.Common;

namespace ApexPost.Domain.Catalogue;

public sealed class ServiceDefinition
{
    public ServiceDefinition(EntityId id, ServiceCode code, string name, string description)
    {
        Id = id ?? throw new DomainException("Service identity is required.");
        Code = code ?? throw new DomainException("Service code is required.");
        Name = RequiredText(name, "Service name", 120);
        Description = RequiredText(description, "Service description", 500);
        Availability = ServiceAvailability.Available;
    }

    public EntityId Id { get; }

    public ServiceCode Code { get; }

    public string Name { get; private set; }

    public string Description { get; private set; }

    public ServiceAvailability Availability { get; private set; }

    public void UpdateDetails(string name, string description)
    {
        EnsureNotRetired();
        Name = RequiredText(name, "Service name", 120);
        Description = RequiredText(description, "Service description", 500);
    }

    public void MakeUnavailable()
    {
        if (Availability != ServiceAvailability.Available)
        {
            throw new DomainException("Only an available service can be made unavailable.");
        }

        Availability = ServiceAvailability.Unavailable;
    }

    public void MakeAvailable()
    {
        if (Availability != ServiceAvailability.Unavailable)
        {
            throw new DomainException("Only an unavailable service can be made available.");
        }

        Availability = ServiceAvailability.Available;
    }

    public void Retire()
    {
        EnsureNotRetired();
        Availability = ServiceAvailability.Retired;
    }

    public static ServiceDefinition Restore(EntityId id, ServiceCode code, string name, string description, ServiceAvailability availability)
    {
        var service = new ServiceDefinition(id, code, name, description);
        if (availability == ServiceAvailability.Unavailable) service.MakeUnavailable();
        else if (availability == ServiceAvailability.Retired) service.Retire();
        else if (availability != ServiceAvailability.Available) throw new DomainException("Service availability must be recognized.");
        return service;
    }

    private static string RequiredText(string value, string fieldName, int maximumLength)
    {
        var normalizedValue = value?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedValue) || normalizedValue.Length > maximumLength)
        {
            throw new DomainException($"{fieldName} is required and must not exceed {maximumLength} characters.");
        }

        return normalizedValue;
    }

    private void EnsureNotRetired()
    {
        if (Availability == ServiceAvailability.Retired)
        {
            throw new DomainException("A retired service cannot be changed.");
        }
    }
}
