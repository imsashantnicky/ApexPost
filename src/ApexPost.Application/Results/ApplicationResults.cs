namespace ApexPost.Application.Results;

public static class ApplicationResults
{
    public static ApplicationResult<T> Success<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ApplicationResult<T>(true, value, []);
    }

    public static ApplicationResult<T> Failure<T>(IEnumerable<ValidationError> errors)
    {
        return new ApplicationResult<T>(false, default, ValidationErrorCollection.Create(errors));
    }
}
