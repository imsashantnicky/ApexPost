using ApexPost.Web.Extensions;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace ApexPost.Web.Tests;

public sealed class SecurityAndCSharp14Tests
{
    [Fact]
    public async Task SecurityHeadersMiddlewareAddsBaselineHeaders()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new SecurityHeadersMiddleware(async httpContext =>
        {
            await httpContext.Response.StartAsync();
        });

        await middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Contains("default-src 'self'", context.Response.Headers["Content-Security-Policy"].ToString());
        Assert.Equal("camera=(), microphone=(), geolocation=()", context.Response.Headers["Permissions-Policy"].ToString());
    }

    [Theory]
    [InlineData("ApexPost", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void CSharp14ExtensionMemberIsNotBlankReturnsExpectedResult(string value, bool expected)
    {
        Assert.Equal(expected, value.IsNotBlank);
    }

    [Fact]
    public void CSharp14ExtensionMemberIsNotBlankHandlesNull()
    {
        string? value = null;

        Assert.False(value.IsNotBlank);
    }
}
