using IntegrationTest.Common;
using QsMessaging.Public;

namespace CleanupArrangeInstance01;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddHostedService<Worker>();
        builder.Services.AddConfiguredQsMessaging(builder.Configuration);
        builder.Services.AddSingleton<ITransportCleanupTestAdministration, TransportCleanupTestAdministration>();
        builder.Services.AddSingleton<ITransportCleanupCheckpointStore, FileTransportCleanupCheckpointStore>();
        builder.Services.AddSingleton<ITransportCleanupExecutor, TransportCleanupExecutor>();
        builder.Services.AddTransient<IScenario, CleanUpTransportationScenario>();
        builder.Services.AddTransient<IScenario, FullCleanUpTransportationScenario>();

        var host = builder.Build();
        var administration = host.Services.GetRequiredService<ITransportCleanupTestAdministration>();
        foreach (var scenario in TransportCleanupScenarioCatalog.All)
        {
            await administration.PrepareScenarioAsync(scenario);
        }

        await host.UseQsMessaging();
        await host.RunAsync();
    }
}
