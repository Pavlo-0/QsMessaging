using QsMessaging.RabbitMq.Models.Enums;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IRqChannelService
    {
        Task CloseByConnectionAsync(IConnection connection);

        Task<IChannel> GetOrCreateChannelAsync(RqChannelPurpose purpose, CancellationToken cancellationToken = default);
    }
}
