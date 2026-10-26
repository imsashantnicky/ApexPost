using ApexPost.Web.Controllers;
using ApexPost.Web.Filters;
using ApexPost.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Tests;

public sealed class SecurityAndActionFilterTests
{
    [Fact]
    public void AuditAndPerformanceFiltersImplementAsyncActionFilter()
    {
        Assert.Contains(typeof(IAsyncActionFilter), typeof(AuditActionFilter).GetInterfaces());
        Assert.Contains(typeof(IAsyncActionFilter), typeof(PerformanceActionFilter).GetInterfaces());
        Assert.Contains(typeof(IAsyncActionFilter), typeof(ValidationActionFilter).GetInterfaces());
    }

    [Fact]
    public void MoneyOrderControllerOptsIntoValidationFilter()
    {
        var attribute = Assert.Single(
            typeof(MoneyOrdersController)
                .GetCustomAttributes(typeof(ServiceFilterAttribute), inherit: true)
                .Cast<ServiceFilterAttribute>());

        Assert.Equal(typeof(ValidationActionFilter), attribute.ServiceType);
    }

    [Fact]
    public async Task SecurityHeadersMiddlewareAddsBrowserSecurityHeaders()
    {
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Contains("frame-ancestors 'none'", context.Response.Headers["Content-Security-Policy"].ToString());
    }
}
