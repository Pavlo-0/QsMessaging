using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Models
{
    internal record StoreConsumerRecord(IChannel Channel, string QueueName, string ConsumerTag);
}
