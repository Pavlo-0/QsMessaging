namespace CleanupAssertInstance01;

internal sealed class RunTestWorker(
    IEnumerable<IScenario> scenarios,
    ILogger<RunTestWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(2000, stoppingToken);

        foreach (var scenario in scenarios)
        {
            try
            {
                await scenario.Run();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error occurred while running assert scenario {ScenarioName}", scenario.GetType().Name);
            }
        }
    }
}
