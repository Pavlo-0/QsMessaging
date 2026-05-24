using QsMessaging.AzureServiceBus;
using QsMessaging.RabbitMq;
using System.Reflection;

namespace QsMessaging.Public
{
    public interface IQsMessagingConfiguration
    {
        ICollection<Assembly> AssembliesToScan { get; }
        QsAzureServiceBusConfiguration AzureServiceBus { get; set; }
        QsMessageHandlerRetryConfiguration HandlerResilience { get; set; }
        QsRabbitMQConfiguration RabbitMQ { get; set; }
        QsMessageReceiverRetryConfiguration Resilience { get; set; }
        QsMessagingTransport Transport { get; set; }
        bool AllowDangerousFullCleanup { get; set; }
        int RequestResponseTimeout { get; set; }
        string ServiceName { get; set; }
    }
}
