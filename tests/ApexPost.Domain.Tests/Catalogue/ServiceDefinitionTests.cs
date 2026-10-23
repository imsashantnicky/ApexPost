using ApexPost.Domain.Catalogue;
using ApexPost.Domain.Common;

namespace ApexPost.Domain.Tests.Catalogue;

public class ServiceDefinitionTests
{
    [Fact]
    public void ConstructorWithValidValuesCreatesAvailableService()
    {
        var service = CreateService();

        Assert.Equal("EXP-001", service.Code.Value);
        Assert.Equal("Express Parcel", service.Name);
        Assert.Equal(ServiceAvailability.Available, service.Availability);
    }

    [Theory]
    [InlineData("")]
    [InlineData("EXP-1")]
    [InlineData("E-001")]
    [InlineData("EXP-00A")]
    public void ServiceCodeWithInvalidFormatThrowsDomainException(string value)
    {
        Assert.Throws<DomainException>(() => ServiceCode.From(value));
    }

    [Fact]
    public void ConstructorWithBlankNameThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new ServiceDefinition(
            EntityId.New(),
            ServiceCode.From("EXP-001"),
            " ",
            "Priority parcel delivery."));
    }

    [Fact]
    public void MakeUnavailableThenAvailableChangesAvailability()
    {
        var service = CreateService();

        service.MakeUnavailable();
        service.MakeAvailable();

        Assert.Equal(ServiceAvailability.Available, service.Availability);
    }

    [Fact]
    public void RetiredServiceCannotBeReactivatedOrUpdated()
    {
        var service = CreateService();
        service.Retire();

        Assert.Equal(ServiceAvailability.Retired, service.Availability);
        Assert.Throws<DomainException>(service.MakeAvailable);
        Assert.Throws<DomainException>(() => service.UpdateDetails("New name", "New description"));
    }

    private static ServiceDefinition CreateService()
    {
        return new ServiceDefinition(
            EntityId.New(),
            ServiceCode.From("EXP-001"),
            "Express Parcel",
            "Priority parcel delivery.");
    }
}
