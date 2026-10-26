using System.Net;
using Microsoft.AspNetCore.Http;

namespace ApexPost.Web.Middleware;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class IntegrationIpRestrictedAttribute : Attribute;

public sealed class IntegrationIpAllowlistMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private const string IntegrationRoute = "/api/integrations/money-orders";
    private readonly HashSet<IPAddress> allowed = ParseAddresses(configuration.GetSection("ApexPost:Security:IntegrationAllowedIPs").Get<string[]>());

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IntegrationIpRestrictedAttribute>() is null
            && !context.Request.Path.StartsWithSegments(IntegrationRoute, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var address = Normalize(context.Connection.RemoteIpAddress);
        if (allowed.Count == 0 || address is null || !allowed.Contains(address))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "The request is not permitted.").ExecuteAsync(context);
            return;
        }

        await next(context);
    }

    private static HashSet<IPAddress> ParseAddresses(IEnumerable<string>? values)
    {
        var addresses = new HashSet<IPAddress>();
        foreach (var value in values ?? [])
        {
            if (!IPAddress.TryParse(value, out var address))
                throw new InvalidOperationException("Integration IP allowlist contains an invalid address.");
            addresses.Add(Normalize(address)!);
        }
        return addresses;
    }

    private static IPAddress? Normalize(IPAddress? address) => address?.IsIPv4MappedToIPv6 == true ? address.MapToIPv4() : address;
}
