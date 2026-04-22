using Microsoft.Extensions.Logging;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Shared;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services.Interfaces;

namespace QsMessaging.RabbitMq
{
    internal class RqSubscriber(
        ILogger<RqSubscriber> logger,
        IRqConnectionService connectionService,
        IRqChannelService channelService,
        IRqExchangeService exchangeService,
        IRqQueueService queueService,
        IHandlerService handlerService,
        IRqConsumerService consumerService) : ISubscriber
    {
        public async Task SubscribeAsync(CancellationToken cancellationToken = default)
        {
            foreach (var record in handlerService.GetHandlers())
            {
                await SubscribeHandlerAsync(record, cancellationToken);
            }
        }

        public async Task SubscribeHandlerAsync(HandlersStoreRecord record, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Subscribing handler to the message queue.");
            logger.LogDebug("{Type}", record.GenericType.FullName);

            var channelPurpose = HardConfiguration.GetChannelPurpose(record.supportedInterfacesType);
            var exchangePurpose = HardConfiguration.GetExchangePurpose(record.supportedInterfacesType);
            var queueType = HardConfiguration.GetQueuePurpose(record.supportedInterfacesType);

            var connection = await connectionService.GetOrCreateConnectionAsync(cancellationToken);
            var channel = await channelService.GetOrCreateChannelAsync(channelPurpose, cancellationToken);
            var exchangeName = await exchangeService.GetOrCreateExchangeAsync(channel, record.GenericType, exchangePurpose, cancellationToken);
            var queueName = await queueService.GetOrCreateQueuesAsync(channel, record.GenericType, exchangeName, queueType, cancellationToken);

            await consumerService.GetOrCreateConsumerAsync(channel, queueName, record);
        }
    }
}
