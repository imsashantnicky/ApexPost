using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using ApexPost.Application.Staff;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ApexPost.Web.Services;

public static class IntegrationApiKeyDefaults
{
    public const string Scheme = "ApexPostIntegrationKey";
}

public sealed class IntegrationApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration,
    IStaffRepository staffRepository)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string HeaderName = "X-API-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredKey = configuration["ApexPost:Integration:ApiKey"];
        var configuredStaffId = configuration["ApexPost:Integration:StaffId"];
        if (string.IsNullOrWhiteSpace(configuredKey) || !Guid.TryParse(configuredStaffId, out var staffId))
            return AuthenticateResult.Fail("Integration authentication is unavailable.");

        if (!Request.Headers.TryGetValue(HeaderName, out var suppliedValues) || suppliedValues.Count != 1 || string.IsNullOrEmpty(suppliedValues[0]))
            return AuthenticateResult.Fail("Invalid integration credentials.");

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedValues[0]!));
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash))
            return AuthenticateResult.Fail("Invalid integration credentials.");

        var staff = await staffRepository.GetByIdAsync(EntityId.From(staffId), Context.RequestAborted);
        if (staff is null || staff.Value.Status != EmploymentStatus.Active || !staff.Value.Roles.Contains(StaffRole.CounterClerk))
            return AuthenticateResult.Fail("Integration authentication is unavailable.");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, staff.Value.Id.Value.ToString("D")),
            new Claim(ClaimTypes.Name, "ApexPost integration counter"),
            new Claim(ClaimTypes.Role, LocalAccountService.CounterStaffRole),
            new Claim(ActorClaimTypes.StaffId, staff.Value.Id.Value.ToString("D")),
            new Claim(ActorClaimTypes.AssignedBranchId, staff.Value.BranchId.Value.ToString("D"))
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
