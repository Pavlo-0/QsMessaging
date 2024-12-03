using Contract.RequestResponse;
using QsMessaging.Public.Handler;

namespace RequestResponse.ResponseInstance.RequestHandler
{
    internal class RequestSumHandler(ILogger<RequestSumHandler> logger) : IQsRequestResponseHandler<QsmTermsContract, QsmAnswerContract>
    {
        public Task<QsmAnswerContract> Consumer(QsmTermsContract request)
        {
            logger.LogInformation("Receive request: {time}", DateTimeOffset.Now);   

            return Task.FromResult(new QsmAnswerContract
            {
                SumAnswer = request.Number1 + request.Number2
            });
        }
    }
}
