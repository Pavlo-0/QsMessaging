

namespace ArrangeInstance01
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IEnumerable<IScenario> _scenarios;

        public Worker(ILogger<Worker> logger, IEnumerable<IScenario> scenarios)
        {
            _logger = logger;
            _scenarios = scenarios;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("------ Run tests: {time} -------", DateTimeOffset.Now);

                foreach (var scenario in _scenarios)
                {
                    await scenario.Run();
                    _logger.LogInformation("Test {Name} executed", scenario.GetType().Name);
                }

                await Task.Delay(2000, stoppingToken);
            }
        }
    }}
