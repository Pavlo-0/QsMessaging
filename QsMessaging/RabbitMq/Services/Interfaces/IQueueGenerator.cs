using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services
{
    internal interface IQueueGenerator
    {
        Task<string> CreateQueues(IChannel channel, Type TModel, string exchangeName, QueueType queueType);
    }
}