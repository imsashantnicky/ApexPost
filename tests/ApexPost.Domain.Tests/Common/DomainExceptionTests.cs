using ApexPost.Domain.Common;

namespace ApexPost.Domain.Tests.Common;

public class DomainExceptionTests
{
    [Fact]
    public void ConstructorPreservesMessageAndInnerException()
    {
        var innerException = new InvalidOperationException("Storage failure");

        var exception = new DomainException("A domain rule was violated.", innerException);

        Assert.Equal("A domain rule was violated.", exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }
}
