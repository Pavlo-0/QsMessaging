

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
            Console.WriteLine($"------ Run all tests: {DateTimeOffset.Now} -------");

            foreach (var scenario in _scenarios)
            {
                await scenario.Run();
                Console.WriteLine($"Test {scenario.GetType().Name} executed");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                Console.WriteLine($"------ Run repeatable tests: {DateTimeOffset.Now} -------");

                foreach (var scenario in _scenarios.Where(s=>s.IsRepeatable))
                {
                    await scenario.Run();
                    Console.WriteLine($"Test {scenario.GetType().Name} executed");
                }

                await Task.Delay(2000, stoppingToken);
            }
        }
    }}
