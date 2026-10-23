using ApexPost.Domain.Common;

namespace ApexPost.Domain.Tests.Common;

public class EntityIdTests
{
    [Fact]
    public void FromWithSameValueCreatesEqualIdentifiers()
    {
        var value = Guid.Parse("7B40B1D4-772D-4DFF-B716-FB8F5AB9D948");

        var first = EntityId.From(value);
        var second = EntityId.From(value);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void FromWithDifferentValuesCreatesDifferentIdentifiers()
    {
        var first = EntityId.From(Guid.Parse("7B40B1D4-772D-4DFF-B716-FB8F5AB9D948"));
        var second = EntityId.From(Guid.Parse("C678ECF7-8E76-407B-B0BD-529039B3D7E2"));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void NewCreatesANonEmptyIdentifier()
    {
        var identifier = EntityId.New();

        Assert.NotEqual(Guid.Empty, identifier.Value);
    }

    [Fact]
    public void FromWithEmptyValueThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => EntityId.From(Guid.Empty));

        Assert.Equal("An entity identifier cannot be empty.", exception.Message);
    }
}
