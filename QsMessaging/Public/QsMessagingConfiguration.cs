
using System.Reflection;

namespace QsMessaging.Public
{
    public class QsMessagingConfiguration
    {
        public QsMessagingConfiguration()
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
        /// Configuration settings related to the connection with the RabbitMQ instance.
        /// </summary>
        public QsRabbitMQConfiguration RabbitMQ { get; set; } = new QsRabbitMQConfiguration();

        /// <summary>
        /// Allows you to set a custom displayed service name. By default, it uses your assembly name.
        /// </summary>
        public string ServiceName { get; set; }
    }

    public class QsRabbitMQConfiguration
    {
        public string Host { get; set; } = "localhost";
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public int Port { get; set; } = 5672;
    }
}
