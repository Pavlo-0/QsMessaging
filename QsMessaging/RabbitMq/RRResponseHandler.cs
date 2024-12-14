using Microsoft.Extensions.Logging;
using QsMessaging.RabbitMq.Interfaces;

namespace QsMessaging.RabbitMq
{
    internal class RRResponseHandler(ILogger<RRResponseHandler> logger, IRequestResponseMessageStore store) : IRRResponseHandler
    {
        public Task Consumer(object contract, string correlationId)
        {
            try
            {
                store.MarkAsResponded(correlationId, contract);
            }
            catch (Exception ex) 
            {
                logger.LogCritical(ex, "An unexpected internal error occurred while marking the message as responded.");
            }
            return Task.CompletedTask;
        }
    }
}
