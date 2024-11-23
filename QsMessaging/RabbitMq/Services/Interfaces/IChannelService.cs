using RabbitMQ.Client;
using static QsMessaging.RabbitMq.Services.ChannelService;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IChannelService
    {
        IEnumerable<IChannel> GetByConnection(IConnection connection);
        Task<IChannel> GetOrCreateChannelAsync(IConnection connection, ChannelPurpose purpose, CancellationToken cancellationToken = default);
    }
}