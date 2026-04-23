using IntegrationTest.Common;

namespace CleanupAssertInstance01;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddHostedService<Worker>();
        builder.Services.AddHostedService<RunTestWorker>();
        builder.Services.AddSingleton<ITransportCleanupTestAdministration, TransportCleanupTestAdministration>();
        builder.Services.AddSingleton<ITransportCleanupCheckpointStore, FileTransportCleanupCheckpointStore>();
        builder.Services.AddTransient<IScenario, CleanUpTransportationScenario>();
        builder.Services.AddTransient<IScenario, FullCleanUpTransportationScenario>();

        var host = builder.Build();

        foreach (CleanupTestScenario value in Enum.GetValues(typeof(CleanupTestScenario)))
        {
            CollectionTestResults.AddTest(value);
        }

        await host.RunAsync();
    }
}
