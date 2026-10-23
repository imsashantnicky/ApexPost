namespace ApexPost.Application.Results;

internal static class ValidationErrorCollection
{
    public static IReadOnlyList<ValidationError> Create(IEnumerable<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var result = errors.ToArray();

        if (result.Length == 0)
        {
            throw new ArgumentException("A failed application result must contain at least one validation error.", nameof(errors));
        }

        if (result.Any(error => error is null))
        {
            throw new ArgumentException("Validation errors cannot contain null values.", nameof(errors));
        }

        return Array.AsReadOnly(result);
    }
}
