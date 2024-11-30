using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services
{
    internal interface IQueueService
    {
        Task<string> GetOrCreateQueuesAsync(IChannel channel, Type TModel, string exchangeName, QueueType queueType);
    }
}