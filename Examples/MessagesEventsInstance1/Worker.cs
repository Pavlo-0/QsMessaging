using Contract.MessagesEventsInstance;
using QsMessaging.Public;

namespace MessagesEventsInstance1
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        private readonly IQsMessaging _qsMessaging;

        public Worker(ILogger<Worker> logger, IQsMessaging qsMessaging)
        {
            _logger = logger;
            _qsMessaging = qsMessaging;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }

                await _qsMessaging.SendMessageAsync(new RegularMessageContract
                {
                    MyTextMessage = "Text Text Text Text Text Text Text Text "
                });


                await Task.Delay(1000 * 5, stoppingToken);
            }
        }
    }
}
