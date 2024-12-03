using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Models
{
    internal record StoreQueueRecord(IChannel Channel, Type TModel, string ExchangeName, string QueueName);
}