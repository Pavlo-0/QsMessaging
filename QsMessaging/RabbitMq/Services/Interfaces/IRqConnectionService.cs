using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IRqConnectionService
    {
        Task CloseAsync(CancellationToken cancellationToken = default);

        IConnection? GetConnection();

        Task<IConnection> GetOrCreateConnectionAsync(CancellationToken cancellationToken = default);
    }
}
