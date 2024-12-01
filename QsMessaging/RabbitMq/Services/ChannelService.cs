using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;
using System.Collections.Concurrent;

namespace QsMessaging.RabbitMq.Services
{
    internal class ChannelService(IConnectionService connectionService): IChannelService
    {
        private static ConcurrentDictionary<ChannelPurpose, (IConnection connection, IChannel channel)> _channels
            = new ConcurrentDictionary<ChannelPurpose, (IConnection connection, IChannel channel)>();


        public async Task<IChannel> GetOrCreateChannelAsync(IConnection connection, ChannelPurpose purpose, CancellationToken cancellationToken = default)
        {
            if (_channels.TryGetValue(purpose, out var connectionAndChannel) &&
                connectionAndChannel.connection != null && connectionAndChannel.connection.IsOpen &&
                connectionAndChannel.channel != null && connectionAndChannel.channel.IsOpen)
            {
                return connectionAndChannel.channel;
            }

            var newConnection = await connectionService.GetOrCreateConnectionAsync(cancellationToken);
            
            var newChannel = await newConnection.CreateChannelAsync();
            //TODO: implement iteration
            if (newChannel == null)
            {
                throw new InvalidOperationException("Failed to create a new channel.");
            }

            _channels.AddOrUpdate(purpose, (newConnection, newChannel), (key, value) => (newConnection, newChannel));

            return newChannel;
        }

        public IEnumerable<IChannel> GetByConnection(IConnection connection)
        {
            return _channels.Where(r => r.Value.connection == connection).Select(r => r.Value.channel);
        }
    }

    public enum ChannelPurpose
    {
        Common,
        MessagePublish,
        EventPublish,
        QueuePermanent,
        QueueConsumerTemporary,
        QueueInstanceTemporary,
        QueueSingleTemporary,
    }
}
