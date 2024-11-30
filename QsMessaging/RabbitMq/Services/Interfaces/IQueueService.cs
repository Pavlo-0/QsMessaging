using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services
{
    internal interface IQueueService
    {
        Task<string> GetOrCreateQueues(IChannel channel, Type TModel, string exchangeName, QueueType queueType);
    }
}