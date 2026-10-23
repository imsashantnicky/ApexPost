using ApexPost.Application.Results;

namespace ApexPost.Application.Tests.Results;

public class ApplicationResultTests
{
    [Fact]
    public void SuccessHasNoErrors()
    {
        var result = ApplicationResult.Success();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void GenericSuccessExposesValueAndNoErrors()
    {
        var result = ApplicationResults.Success("BR-001");

        Assert.True(result.IsSuccess);
        Assert.Equal("BR-001", result.Value);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void FailurePreservesValidationErrors()
    {
        var error = new ValidationError("branch.name.required", "Branch name is required.", "name");

        var result = ApplicationResult.Failure([error]);

        Assert.False(result.IsSuccess);
        Assert.Equal(error, Assert.Single(result.Errors));
    }

    [Fact]
    public void GenericFailureDoesNotExposeValue()
    {
        var result = ApplicationResults.Failure<string>([
            new ValidationError("branch.name.required", "Branch name is required.", "name")
        ]);

        Assert.False(result.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void FailureRequiresAtLeastOneValidationError()
    {
        Assert.Throws<ArgumentException>(() => ApplicationResult.Failure([]));
        Assert.Throws<ArgumentException>(() => ApplicationResults.Failure<string>([]));
    }

    [Fact]
    public void ValidationErrorRequiresCodeAndMessage()
    {
        Assert.Throws<ArgumentException>(() => new ValidationError(" ", "Message"));
        Assert.Throws<ArgumentException>(() => new ValidationError("code", " "));
    }
}
