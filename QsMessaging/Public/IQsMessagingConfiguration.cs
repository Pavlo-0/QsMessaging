using QsMessaging.AzureServiceBus;
using QsMessaging.RabbitMq;
using System.Reflection;
using System.Text.Json;

namespace QsMessaging.Public
{
    public interface IQsMessagingConfiguration
    {
        ICollection<Assembly> AssembliesToScan { get; }
        QsAzureServiceBusConfiguration AzureServiceBus { get; set; }
        QsMessageHandlerRetryConfiguration HandlerResilience { get; set; }
        QsRabbitMQConfiguration RabbitMQ { get; set; }
        QsMessageReceiverRetryConfiguration Resilience { get; set; }
        QsMessagingSerializationConfiguration Serialization { get; set; }
        QsMessagingTransport Transport { get; set; }
        bool AllowDangerousFullCleanup { get; set; }
        int RequestResponseTimeout { get; set; }
        string ServiceName { get; set; }
    }

    public class QsMessagingSerializationConfiguration
    {
        public JsonSerializerOptions JsonSerializerOptions { get; set; } = new();
        public string ContentType { get; set; } = "application/json";
        public string ContentEncoding { get; set; } = "utf-8";
        public string ContractVersion { get; set; } = "1";
    }
}
