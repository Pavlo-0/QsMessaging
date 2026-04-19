using Microsoft.Extensions.Logging;
using QsMessaging.Shared.Interface;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.RabbitMq.Models;
using QsMessaging.Shared.Services.Interfaces;
using QsMessaging.Shared;

namespace QsMessaging.RabbitMq
{
    internal class RqSubscriber(
        ILogger<RqSubscriber> logger,
        IRbConnectionService connectionService,
        IChannelService channelService,
        IExchangeService exchangeService,
        IQueueService queueService,
        IHandlerService handlerService,
        IConsumerService consumerService) : ISubscriber
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

            var connection = await connectionService.GetOrCreateConnectionAsync();
            var channel = await channelService.GetOrCreateChannelAsync(connection, channelPurpose);

            var exchangename = await exchangeService.GetOrCreateExchangeAsync(channel, record.GenericType, exchangePurpose);
            var queueName = await queueService.GetOrCreateQueuesAsync(channel, record.GenericType, exchangename, queueType);

            await consumerService.GetOrCreateConsumerAsync(channel, queueName, record);
        }
    }
}
