namespace AssertInstance01
{
    public class RunTestWorker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IEnumerable<IScenario> _scenarios;

        public RunTestWorker(ILogger<Worker> logger, IEnumerable<IScenario> scenarios)
        {
            _logger = logger;
            _scenarios = scenarios;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(2000, stoppingToken);

            foreach (var scenario in _scenarios)
            {
                await scenario.Run();
            }
        }
    }
}
