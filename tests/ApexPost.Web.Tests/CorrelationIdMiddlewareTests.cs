using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Http;

namespace ApexPost.Web.Tests;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task UsesSafeIncomingCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "test-id_123.abc";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal("test-id_123.abc", context.TraceIdentifier);
        Assert.Equal("test-id_123.abc", context.Response.Headers["X-Correlation-ID"].ToString());
    }

    [Theory]
    [InlineData("bad value with spaces")]
    [InlineData("bad\\r\\nX-Injected: yes")]
    [InlineData("")]
    public async Task ReplacesUnsafeIncomingCorrelationId(string incomingId)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = incomingId;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var correlationId = context.Response.Headers["X-Correlation-ID"].ToString();
        Assert.NotEmpty(correlationId);
        Assert.DoesNotContain(" ", correlationId);
        Assert.DoesNotContain("\\r", correlationId);
        Assert.DoesNotContain("\\n", correlationId);
        Assert.Equal(correlationId, context.TraceIdentifier);
    }

    [Fact]
    public async Task ReplacesOverlongIncomingCorrelationId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = new string('a', 100);
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.NotEqual(new string('a', 100), context.TraceIdentifier);
        Assert.True(context.TraceIdentifier.Length <= 64);
    }
    [Fact]
    public async Task GeneratesCorrelationIdWhenIncomingValueIsMissing()
    {
        var context = new DefaultHttpContext();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        var correlationId = context.Response.Headers["X-Correlation-ID"].ToString();

        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.True(correlationId.Length <= 64);
        Assert.Equal(correlationId, context.TraceIdentifier);
    }
}
