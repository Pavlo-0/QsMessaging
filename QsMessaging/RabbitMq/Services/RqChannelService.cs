using Microsoft.Extensions.Logging;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;
using System.Collections.Concurrent;

namespace QsMessaging.RabbitMq.Services
{
    internal class RqChannelService(
        ILogger<RqChannelService> logger,
        IRqConnectionService connectionService) : IRqChannelService
    {
        private readonly ConcurrentDictionary<RqChannelPurpose, (IConnection connection, IChannel channel)> _channels = new();
        private readonly ConcurrentDictionary<RqChannelPurpose, SemaphoreSlim> _locks = new();

        public async Task CloseByConnectionAsync(IConnection connection)
        {
            var channelsToClose = _channels
                .Where(pair => pair.Value.connection == connection)
                .ToList();

            await Parallel.ForEachAsync(channelsToClose, async (channelToClose, cancellationToken) =>
            {
                var semaphore = _locks.GetOrAdd(channelToClose.Key, _ => new SemaphoreSlim(1, 1));
                await semaphore.WaitAsync();

                try
                {
                    if (!_channels.TryRemove(channelToClose.Key, out var storedChannel))
                    {
                        return;
                    }

                    try
                    {
                        if (storedChannel.channel.IsOpen)
                        {
                            await storedChannel.channel.CloseAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to close RabbitMQ channel for {Purpose}.", channelToClose.Key);
                    }
                    finally
                    {
                        await storedChannel.channel.DisposeAsync();
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });
        }

        public async Task<IChannel> GetOrCreateChannelAsync(
            RqChannelPurpose purpose,
            CancellationToken cancellationToken = default)
        {
            if (_channels.TryGetValue(purpose, out var existing) &&
                existing.connection.IsOpen &&
                existing.channel.IsOpen)
            {
                return existing.channel;
            }

            var semaphore = _locks.GetOrAdd(purpose, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                if (_channels.TryGetValue(purpose, out existing) &&
                    existing.connection.IsOpen &&
                    existing.channel.IsOpen)
                {
                    return existing.channel;
                }

                logger.LogTrace("Creating RabbitMQ channel for purpose {Purpose}", purpose);

                var connection = await connectionService.GetOrCreateConnectionAsync(cancellationToken);
                var channel = await connection.CreateChannelAsync(options: null, cancellationToken); //TODO: Low: Add options if needed
                if (channel == null)
                {
                    logger.LogCritical("Failed to create RabbitMQ channel for purpose {Purpose}", purpose);
                    throw new InvalidOperationException("Failed to create a new channel.");
                }

                if (_channels.TryGetValue(purpose, out var oldValue))
                {
                    await oldValue.channel.DisposeAsync();
                }

                _channels[purpose] = (connection, channel);

                return channel;
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}