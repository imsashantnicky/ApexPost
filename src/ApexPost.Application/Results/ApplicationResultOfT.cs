namespace ApexPost.Application.Results;

public sealed class ApplicationResult<T>
{
    private readonly T? value;

    internal ApplicationResult(bool isSuccess, T? value, IReadOnlyList<ValidationError> errors)
    {
        IsSuccess = isSuccess;
        this.value = value;
        Errors = errors;
    }

    public bool IsSuccess { get; }

    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("A failed application result does not have a value.");

    public IReadOnlyList<ValidationError> Errors { get; }

}
