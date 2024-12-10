using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Models;

namespace QsMessaging.RabbitMq
{
    internal class Subscriber(
        IServiceProvider services,
        IConnectionService connectionService,
        IChannelService channelService,
        IExchangeService exchangeService,
        IQueueService queueService,
        IHandlerService handlerService,
        IConsumerService consumerService) : ISubscriber
    {

        public async Task SubscribeAsync()
        {
            foreach (var record in handlerService.GetHandlers())
            {
                await SubscribeHandlerAsync(record);
            }
        }

        public async Task SubscribeHandlerAsync(HandlersStoreRecord record)
        {
            var channelPurpose = HardConfiguration.GetChannelPurpose(record.supportedInterfacesType);
            var exchangePurpose = HardConfiguration.GetExchangePurpose(record.supportedInterfacesType);
            var queueType = HardConfiguration.GetQueuePurpose(record.supportedInterfacesType);

            var connection = await connectionService.GetOrCreateConnectionAsync();
            var channel = await channelService.GetOrCreateChannelAsync(connection, channelPurpose);

            var exchangename = await exchangeService.GetOrCreateExchangeAsync(channel, record.GenericType, exchangePurpose);
            var queueName = await queueService.GetOrCreateQueuesAsync(channel, record.GenericType, exchangename, queueType);

            await consumerService.GetOrCreateConsumerAsync(channel, queueName, services, record);
        }
    }
}
