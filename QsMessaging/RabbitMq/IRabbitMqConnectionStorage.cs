using RabbitMQ.Client;

namespace QsMessaging.RabbitMq
{
    internal interface IRabbitMqConnectionStorage
    {
        Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
    }
}