namespace CleanupArrangeInstance01;

public sealed class Worker(
    ILogger<Worker> logger,
    IEnumerable<IScenario> scenarios,
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(2000, stoppingToken);
        Console.WriteLine($"------ Run cleanup integration scenarios: {DateTimeOffset.Now} -------");

        foreach (var scenario in scenarios)
        {
            Console.Write($"Scenario {scenario.GetType().Name} ... ");

            try
            {
                await scenario.Run();
                Console.WriteLine("executed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while running scenario {ScenarioName}", scenario.GetType().Name);
                Console.WriteLine("failed");
            }
        }

        applicationLifetime.StopApplication();
    }
}
