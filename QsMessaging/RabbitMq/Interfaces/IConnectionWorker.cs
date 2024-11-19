using RabbitMQ.Client;

namespace QsMessaging.Public
{
    internal interface IConnectionWorker
    {
        (IConnection? connection, IChannel? chanel) GetConnection();
        Task<(IConnection connection, IChannel chanel)> GetOrCreateConnectionAsync(CancellationToken cancellationToken = default);
    }
}