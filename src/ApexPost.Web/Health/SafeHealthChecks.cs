using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApexPost.Web.Health;

public sealed class SafeReadinessCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy("Application ready."));
    }
}

public sealed class SafeLivenessCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy("Application alive."));
    }
}
