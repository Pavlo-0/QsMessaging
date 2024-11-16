namespace AssertInstance01
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.Clear();
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = CollectionTestResults.GetAllTests();

                //Console.Clear();
                Console.SetCursorPosition(0, 0);
                Console.WriteLine("Test Results:");
                Console.WriteLine("-------------");
                foreach (var test in result)
                {
                    Console.WriteLine($"{test.Key}: \t\t {test.Value}     ");
                }
                Console.WriteLine("-------------");
                await Task.Delay(100, stoppingToken);

                if (result.All(x => x.Value == true))
                {
                    Console.WriteLine("All tests passed.");
                    break;
                }
                else
                {
                    Console.WriteLine("Some test is fail.");
                }
            }

            Console.ReadLine();
        }
    }
}
