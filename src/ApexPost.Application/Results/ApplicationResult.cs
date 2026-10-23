namespace ApexPost.Application.Results;

public sealed class ApplicationResult
{
    private ApplicationResult(bool isSuccess, IReadOnlyList<ValidationError> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    public bool IsSuccess { get; }

    public IReadOnlyList<ValidationError> Errors { get; }

    public static ApplicationResult Success()
    {
        return new ApplicationResult(true, []);
    }

    public static ApplicationResult Failure(IEnumerable<ValidationError> errors)
    {
        return new ApplicationResult(false, ValidationErrorCollection.Create(errors));
    }
}
