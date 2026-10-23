namespace ApexPost.Application.Results;

public sealed record ValidationError
{
    public ValidationError(string code, string message, string? field = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Validation error code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Validation error message is required.", nameof(message));
        }

        Code = code;
        Message = message;
        Field = string.IsNullOrWhiteSpace(field) ? null : field;
    }

    public string Code { get; }

    public string Message { get; }

    public string? Field { get; }
}
