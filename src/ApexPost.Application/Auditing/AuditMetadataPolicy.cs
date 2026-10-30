using System.Collections.Frozen;
using System.Text.RegularExpressions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Catalogue;
using ApexPost.Domain.IdentityVerification;
using ApexPost.Domain.MoneyOrders;
using ApexPost.Domain.Payments;
using ApexPost.Domain.Shipping;
using ApexPost.Domain.Staff;
using ApexPost.Domain.Transactions;

namespace ApexPost.Application.Auditing;

/// <summary>
/// Decides which audit metadata may be persisted. Every key is allow-listed and every value must match the
/// format of that key, so passwords, document numbers, API keys and card data have nowhere to go.
/// Structured values (operation names, correlation IDs, domain status names) are checked by format rather
/// than by a keyword scan: legitimate values such as "Transaction.StartPayment", "PaymentPending" or the
/// correlation ID "monkey-1" contain fragments like "payment" or "key" and must not be rejected.
/// </summary>
public static partial class AuditMetadataPolicy
{
    public const string CorrelationIdKey = "CorrelationId";

    private static readonly string[] SensitiveFragments = ["password", "passwd", "token", "secret", "card", "cvv", "credential", "payment", "document", "identity", "passport", "ssn", "address", "phone", "email", "auth", "session", "key"];

    private static readonly FrozenSet<string> StatusNames = new[]
    {
        typeof(TransactionStatus), typeof(PaymentStatus), typeof(ShipmentStatus), typeof(MoneyOrderStatus),
        typeof(EmploymentStatus), typeof(BranchStatus), typeof(VerificationStatus), typeof(ServiceAvailability)
    }.SelectMany(Enum.GetNames).ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> MethodNames = Enum.GetNames<VerificationMethod>().ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> StaffRoleNames = Enum.GetNames<StaffRole>().ToFrozenSet(StringComparer.Ordinal);

    public static bool IsValidOperationName(string? value) => value is { Length: <= 100 } && OperationNamePattern().IsMatch(value);

    /// <summary>Same rule as CorrelationIdMiddleware: short ASCII letters, digits, '-', '_' and '.'.</summary>
    public static bool IsValidCorrelationId(string? value) => value is not null && CorrelationIdPattern().IsMatch(value);

    public static bool IsAllowed(string key, string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return key switch
        {
            "Operation" => IsValidOperationName(value),
            "Status" => StatusNames.Contains(value) || (ResultCodePattern().IsMatch(value) && !ContainsSensitiveFragment(value)),
            "Method" => MethodNames.Contains(value),
            "Verified" => value is "True" or "False",
            "Quantity" => SignedNumberPattern().IsMatch(value),
            "Version" or "StatusCode" or "ElapsedMs" or "CounterCount" => NumberPattern().IsMatch(value),
            "HttpMethod" => HttpMethodPattern().IsMatch(value),
            "StaffRoles" => value.Split(',').All(StaffRoleNames.Contains),
            "Controller" or "Action" => IdentifierPattern().IsMatch(value),
            "RouteId" => Guid.TryParseExact(value, "D", out _) || NumberPattern().IsMatch(value),
            _ => false
        };
    }

    private static bool ContainsSensitiveFragment(string value) =>
        SensitiveFragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"^[A-Za-z]+(\.[A-Za-z]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex OperationNamePattern();

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex CorrelationIdPattern();

    // Lower-case application result codes such as "concurrency.conflict" or "verification-required".
    [GeneratedRegex(@"^[a-z]+([.-][a-z]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ResultCodePattern();

    // Short enough that a card number (13-19 digits) can never be recorded as a number.
    [GeneratedRegex(@"^\d{1,12}$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^-?\d{1,9}$", RegexOptions.CultureInvariant)]
    private static partial Regex SignedNumberPattern();

    [GeneratedRegex(@"^[A-Z]{3,7}$", RegexOptions.CultureInvariant)]
    private static partial Regex HttpMethodPattern();

    [GeneratedRegex(@"^[A-Za-z]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();
}
