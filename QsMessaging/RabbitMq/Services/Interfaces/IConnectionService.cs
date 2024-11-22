using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IConnectionService
    {
        IConnection? GetConnection();

        Task<IConnection> GetOrCreateConnectionAsync(CancellationToken cancellationToken = default);
    }
}