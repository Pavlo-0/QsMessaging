namespace CleanupAssertInstance01;

public sealed class Worker(
    IHostApplicationLifetime applicationLifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.Clear();

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = CollectionTestResults.GetAllTests();
            Console.SetCursorPosition(0, 0);
            Console.WriteLine("Cleanup Test Results:");
            Console.WriteLine("---------------------");

            foreach (var test in result)
            {
                var testResult = test.Value switch
                {
                    true => "Passed     ",
                    false => "Failed     ",
                    _ => "Progress... "
                };

                Console.WriteLine("{0,-35}{1}", test.Key, testResult);
            }

            Console.WriteLine("---------------------");

            if (result.All(x => x.Value.HasValue))
            {
                if (result.All(x => x.Value == true))
                {
                    Console.WriteLine("All cleanup tests passed.");
                }
                else
                {
                    Console.WriteLine("Some cleanup tests failed.");
                }

                applicationLifetime.StopApplication();
                break;
            }

            await Task.Delay(200, stoppingToken);
        }
        Console.ReadKey(true);
    }
}
