

namespace ArrangeInstance01
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private IEnumerable<IScenario> _scenarios;

        public Worker(ILogger<Worker> logger, IEnumerable<IScenario> scenarios)
        {
            _logger = logger;
            _scenarios = scenarios;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(2000, stoppingToken);
            Console.WriteLine($"------ Run all tests: {DateTimeOffset.Now} -------");

            foreach (var scenario in _scenarios)
            {
                Console.Write($"Test {scenario.GetType().Name} ...");
                await scenario.Run();
                Console.WriteLine($" executed");
            }

            Console.WriteLine($"------ Run repeatable tests: {DateTimeOffset.Now} -------");
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(3000, stoppingToken);

                foreach (var scenario in _scenarios.Where(s=>s.IsRepeatable))
                {
                    Console.Write($"Test {scenario.GetType().Name} ...");
                    await scenario.Run();
                    Console.WriteLine($" executed");
                }
                Console.WriteLine($"----------------------");
            }
        }
    }}
