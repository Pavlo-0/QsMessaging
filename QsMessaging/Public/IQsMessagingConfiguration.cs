using QsMessaging.RabbitMq;

namespace QsMessaging.Public
{
    public interface IQsMessagingConfiguration
    {
        QsRabbitMQConfiguration RabbitMQ { get; set; }
        int RequestResponseTimeout { get; set; }
        string ServiceName { get; set; }
    }
}