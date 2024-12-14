using Microsoft.Extensions.Logging;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;
using System.Collections.Concurrent;

namespace QsMessaging.RabbitMq.Services
{
    internal class ChannelService(ILogger<ChannelService> logger, IConnectionService connectionService): IChannelService
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

            logger.LogTrace("Attempting to create new channel. If need connection as well");

            var newConnection = await connectionService.GetOrCreateConnectionAsync(cancellationToken);
            
            var newChannel = await newConnection.CreateChannelAsync();
            //TODO: implement iteration
            if (newChannel == null)
            {
                logger.LogCritical("Failed to create a new channel.");
                logger.LogDebug("Purpose: {Purpose}", purpose);
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
}
