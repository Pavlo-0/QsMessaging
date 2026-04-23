
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
        /// Configuration settings related to the connection with the RabbitMQ instance.
        /// </summary>
        public QsRabbitMQConfiguration RabbitMQ { get; set; } = new QsRabbitMQConfiguration();

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
