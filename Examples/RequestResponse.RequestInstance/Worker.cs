using Contract.RequestResponse;
using QsMessaging.Public;

namespace RequestResponse.RequestInstance
{
    public class Worker(IQsMessaging qsMessaging, ILogger<Worker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);

                logger.LogInformation("Send request: {time}", DateTimeOffset.Now);
                try
                {
                    var answer = await qsMessaging.RequestResponse<QsmTermsContract, QsmAnswerContract>(new QsmTermsContract
                    {
                        Number1 = 10,
                        Number2 = 20
                    });
                    logger.LogInformation("Get answer {Number} at : {time}", answer.SumAnswer, DateTimeOffset.Now);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while sending request");
                }

                
            }
        }
    }
}
