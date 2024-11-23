
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

        public QsRabbitMQConfiguration RabbitMQ { get; set; } = new QsRabbitMQConfiguration();

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
