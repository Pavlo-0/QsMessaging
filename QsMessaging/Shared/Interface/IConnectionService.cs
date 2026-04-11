using RabbitMQ.Client;

namespace QsMessaging.Shared.Interface
{
    internal interface IConnectionService
    {
        IConnection? GetConnection();

        Task<IConnection> GetOrCreateConnectionAsync(CancellationToken cancellationToken = default);
    }
}