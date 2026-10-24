using ApexPost.Infrastructure.Persistence.Json;

namespace ApexPost.Web.Services;

public sealed class JsonInitializationHostedService : IHostedService
{
    private readonly JsonSeedInitializer initializer;
    private readonly LocalAccountService localAccounts;
    private readonly IWebHostEnvironment environment;
    private readonly IConfiguration configuration;

    public JsonInitializationHostedService(JsonSeedInitializer initializer, LocalAccountService localAccounts, IWebHostEnvironment environment, IConfiguration configuration)
    {
        this.initializer = initializer;
        this.localAccounts = localAccounts;
        this.environment = environment;
        this.configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await initializer.InitializeAsync(cancellationToken);
        await localAccounts.InitializeDevelopmentAccountsAsync(environment, configuration, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
