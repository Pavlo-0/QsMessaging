namespace AssertInstance01
{
    internal class RunTestWorker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IEnumerable<IScenario> _scenarios;
        private readonly IScenarioExecutionGate _scenarioExecutionGate;

        public RunTestWorker(
            ILogger<Worker> logger,
            IEnumerable<IScenario> scenarios,
            IScenarioExecutionGate scenarioExecutionGate)
        {
            _logger = logger;
            _scenarios = scenarios;
            _scenarioExecutionGate = scenarioExecutionGate;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(2000, stoppingToken);

            foreach (var scenario in _scenarios)
            {
                await _scenarioExecutionGate.WaitUntilReadyAsync(stoppingToken);
                await scenario.Run();
            }
        }
    }
}
