using QsMessaging.RabbitMq.Models.Enums;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IRqQueueService
    {
        Task<string> GetOrCreateQueuesAsync(IChannel channel, Type TModel, string exchangeName, RqQueuePurpose queueType, CancellationToken cancellationToken);
    }
}