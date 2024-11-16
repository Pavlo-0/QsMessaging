
namespace QsMessaging.Public
{
    public class QsMessagingConfiguration
    {
        public QsRabbitMQConfiguration RabbitMQ { get; set; } = new QsRabbitMQConfiguration();
    }

    public class QsRabbitMQConfiguration
    {
        public string Host { get; set; } = "localhost";
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public int Port { get; set; } = 5672;
    }
}
