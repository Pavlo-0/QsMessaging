using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Models
{
    internal record RqStoreConsumerRecord(IChannel Channel, string QueueName, string ConsumerTag)
    {
        public CancellationTokenSource CancellationTokenSource { get; init; } = new();
    }
}
