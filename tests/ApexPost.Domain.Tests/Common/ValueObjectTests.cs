using ApexPost.Domain.Common;

namespace ApexPost.Domain.Tests.Common;

public class ValueObjectTests
{
    [Fact]
    public void EqualComponentsCreateEqualValueObjects()
    {
        var first = new PostalCode("560001");
        var second = new PostalCode("560001");

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void DifferentComponentsCreateDifferentValueObjects()
    {
        var first = new PostalCode("560001");
        var second = new PostalCode("110001");

        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    [Fact]
    public void EqualComponentsOfDifferentTypesAreNotEqual()
    {
        var postalCode = new PostalCode("560001");
        var serviceCode = new ServiceCode("560001");

        Assert.False(postalCode.Equals(serviceCode));
    }

    private sealed class PostalCode : ValueObject
    {
        public PostalCode(string value)
        {
            Value = value;
        }

        public string Value { get; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Value;
        }
    }

    private sealed class ServiceCode : ValueObject
    {
        public ServiceCode(string value)
        {
            Value = value;
        }

        public string Value { get; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Value;
        }
    }
}
