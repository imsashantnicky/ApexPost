namespace ApexPost.Web.Extensions;

/// <summary>
/// C# 14 extension members for small, reusable string checks.
/// </summary>
public static class StringExtensions
{
    extension(string? value)
    {
        public bool IsNotBlank => !string.IsNullOrWhiteSpace(value);
    }
}
