using QsMessaging.RabbitMq.Interfaces;

namespace QsMessaging.RabbitMq
{
    internal class RequestResponseResponseHandler(IRequestResponseMessageStore store) : IRequestResponseResponseHandler
    {
        public Task Consumer(object contract, string correlationId)
        {
            try
            {
                store.MarkAsResponded(correlationId, contract);
            }
            catch
            {
                //TODO: Add reaction on error
            }
            return Task.CompletedTask;
        }
    }
}
