using QsMessaging.RabbitMq;

using QsMessaging.AzureServiceBus;

namespace QsMessaging.Public
{
    public interface IQsMessagingConfiguration
    {
        QsAzureServiceBusConfiguration AzureServiceBus { get; set; }
        QsRabbitMQConfiguration RabbitMQ { get; set; }
        QsMessagingTransport Transport { get; set; }
        int RequestResponseTimeout { get; set; }
        string ServiceName { get; set; }
    }
}
