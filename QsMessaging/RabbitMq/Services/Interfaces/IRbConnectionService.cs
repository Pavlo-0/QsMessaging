using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IRbConnectionService
    {
        IConnection? GetConnection();

        Task<IConnection> GetOrCreateConnectionAsync(CancellationToken cancellationToken = default);
    }
}