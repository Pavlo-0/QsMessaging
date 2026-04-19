using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Models
{
    internal record RqStoreQueueRecord(IChannel Channel, Type TModel, string ExchangeName, string QueueName);
}