using ApexPost.Infrastructure.Persistence.Json;

namespace ApexPost.Web.Services;

public sealed class JsonInitializationHostedService : IHostedService
{
    private readonly JsonSeedInitializer initializer;

    public JsonInitializationHostedService(JsonSeedInitializer initializer)
    {
        this.initializer = initializer;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return initializer.InitializeAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
