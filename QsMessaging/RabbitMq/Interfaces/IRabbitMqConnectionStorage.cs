using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Interface
{
    internal interface IRabbitMqConnectionStorage
    {
        Task<(IConnection connection, IChannel chanel)> GetConnectionAsync(CancellationToken cancellationToken = default);
    }
}