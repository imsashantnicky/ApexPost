using Xunit;
using ApexPost.Web.Extensions;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Http;

namespace ApexPost.Web.Tests;

public sealed class SecurityAndCSharp14Tests
{
    [Fact]
    public async Task SecurityHeadersMiddlewareAddsBaselineHeaders()
    {
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Contains("default-src 'self'", context.Response.Headers["Content-Security-Policy"].ToString());
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
