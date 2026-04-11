using QsMessaging.RabbitMq.Models.Enums;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IQueueService
    {
        Task<string> GetOrCreateQueuesAsync(IChannel channel, Type TModel, string exchangeName, QueuePurpose queueType);
    }
}