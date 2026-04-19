using QsMessaging.RabbitMq.Models.Enums;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IRqChannelService
    {
        IEnumerable<IChannel> GetByConnection(IConnection connection);
        Task<IChannel> GetOrCreateChannelAsync(IConnection connection, RqChannelPurpose purpose, CancellationToken cancellationToken = default);
    }
}