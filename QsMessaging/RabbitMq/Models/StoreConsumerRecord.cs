using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services
{
    internal record StoreConsumerRecord(IChannel Channel, string QueueName, string ConsumerTag);
}
