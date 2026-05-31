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
        QsFailedMessageHandlingConfiguration FailedMessageHandling { get; set; }
        QsMessageHandlerRetryConfiguration HandlerResilience { get; set; }
        QsRabbitMQConfiguration RabbitMQ { get; set; }
        QsMessageReceiverRetryConfiguration Resilience { get; set; }
        QsMessagingSerializationConfiguration Serialization { get; set; }
        QsMessagingTransport Transport { get; set; }
        bool AllowDangerousFullCleanup { get; set; }
        int RequestResponseTimeout { get; set; }
        string ServiceName { get; set; }
    }

    public class QsFailedMessageHandlingConfiguration
    {
        /// <summary>
        /// Sends a diagnostic wrapper for failed consumer messages to a dedicated error queue.
        /// Disabled by default to preserve existing transport behavior.
        /// </summary>
        public bool SendToErrorQueue { get; set; }

        /// <summary>
        /// Calls registered IQsMessagingConsumerErrorHandler implementations after consumer failures.
        /// Enabled by default to preserve existing error handler behavior.
        /// </summary>
        public bool CallErrorHandlers { get; set; } = true;
    }

    public class QsMessagingSerializationConfiguration
    {
        public JsonSerializerOptions JsonSerializerOptions { get; set; } = new();
        public string ContentType { get; set; } = "application/json";
        public string ContentEncoding { get; set; } = "utf-8";
        public string ContractVersion { get; set; } = "1";
    }
}
