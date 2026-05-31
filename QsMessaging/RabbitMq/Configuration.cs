
using System.Reflection;
using QsMessaging.AzureServiceBus;
using QsMessaging.Public;

namespace QsMessaging.RabbitMq
{
    internal class Configuration : IQsMessagingConfiguration
    {
        public Configuration()
        {
            try
            {
                ServiceName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Unknown";
            }
            catch
            {
                ServiceName = "Unknown";
            }
        }

        /// <summary>
        /// Configuration settings related to the connection with Azure Service Bus.
        /// </summary>
        public QsAzureServiceBusConfiguration AzureServiceBus { get; set; } = new QsAzureServiceBusConfiguration();

        /// <summary>
        /// Assemblies that QsMessaging scans for consumer handlers and consumer error handlers.
        /// When empty, QsMessaging falls back to the entry assembly and the assembly that called AddQsMessaging.
        /// </summary>
        public ICollection<Assembly> AssembliesToScan { get; } = new List<Assembly>();

        /// <summary>
        /// Configuration settings related to the connection with the RabbitMQ instance.
        /// </summary>
        public QsRabbitMQConfiguration RabbitMQ { get; set; } = new QsRabbitMQConfiguration();

        /// <summary>
        /// Controls how failed consumer messages are routed after handler retries are exhausted.
        /// </summary>
        public QsFailedMessageHandlingConfiguration FailedMessageHandling { get; set; } = new();

        /// <summary>
        /// Retry settings for user message handlers before QsMessaging calls consumer error handlers.
        /// </summary>
        public QsMessageHandlerRetryConfiguration HandlerResilience { get; set; } = new();

        /// <summary>
        /// Retry settings for transport send operations shared by RabbitMQ and Azure Service Bus.
        /// </summary>
        public QsMessageReceiverRetryConfiguration Resilience { get; set; } = new();

        /// <summary>
        /// JSON serialization and message metadata settings shared by all transports.
        /// </summary>
        public QsMessagingSerializationConfiguration Serialization { get; set; } = new();

        /// <summary>
        /// Allows you to set a custom displayed service name. By default, it uses your assembly name.
        /// </summary>
        public string ServiceName { get; set; }

        /// <summary>
        /// How long QsMessaging will be waiting answer from another service. msec.
        /// </summary>
        public int RequestResponseTimeout { get; set; } = 50 * 1000;

        /// <summary>
        /// Active transport used by QsMessaging.
        /// </summary>
        public QsMessagingTransport Transport { get; set; } = QsMessagingTransport.RabbitMq;

        /// <summary>
        /// Allows FullCleanUpTransportation to delete every visible transport entity.
        /// When false, full cleanup is limited to QsMessaging-prefixed entities.
        /// </summary>
        public bool AllowDangerousFullCleanup { get; set; }
    }

    public class QsRabbitMQConfiguration
    {
        public string Host { get; set; } = "localhost";
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public int Port { get; set; } = 5672;
        public string VirtualHost { get; set; } = "/";
        public string ManagementScheme { get; set; } = "http";
        public int ManagementPort { get; set; } = 15672;
        public string? ManagementApiBaseAddress { get; set; }
        public string? ManagementUserName { get; set; }
        public string? ManagementPassword { get; set; }
    }
}
