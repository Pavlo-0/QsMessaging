using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Interface
{
    internal interface IConnectionStorage
    {
        Task<(IConnection connection, IChannel chanel)> GetConnectionAsync(CancellationToken cancellationToken = default);
    }
}