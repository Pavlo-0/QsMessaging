using QsMessaging.RabbitMq.Interfaces;

namespace QsMessaging.RabbitMq
{
    internal class RRResponseHandler(IRequestResponseMessageStore store) : IRRResponseHandler
    {
        public Task Consumer(object contract, string correlationId)
        {
            try
            {
                store.MarkAsResponded(correlationId, contract);
            }
            catch
            {
                //TODO: Correlation could not be found cause this is can be another instances.
            }
            return Task.CompletedTask;
        }
    }
}
