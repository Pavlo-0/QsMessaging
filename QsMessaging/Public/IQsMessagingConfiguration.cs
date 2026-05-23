using QsMessaging.AzureServiceBus;
using QsMessaging.RabbitMq;

namespace QsMessaging.Public
{
    public interface IQsMessagingConfiguration
    {
        QsAzureServiceBusConfiguration AzureServiceBus { get; set; }
        QsMessageHandlerRetryConfiguration HandlerResilience { get; set; }
        QsRabbitMQConfiguration RabbitMQ { get; set; }
        QsMessageReceiverRetryConfiguration Resilience { get; set; }
        QsMessagingTransport Transport { get; set; }
        int RequestResponseTimeout { get; set; }
        string ServiceName { get; set; }
    }
}
