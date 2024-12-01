using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.RabbitMq.Services;

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

        public async Task Subscribe()
        {
            foreach (var record in handlerService.GetHandlers())
            {
                await SubscribeHandlerAsync(record);
            }
        }

        public async Task SubscribeHandlerAsync(
            HandlerService.HandlersStoreRecord record)
        {
            var channelPurpose = HardConfiguration.GetChannelPurposeByInterfaceTypes(record.supportedInterfacesType);
            var queueType = HardConfiguration.GetQueueByInterfaceTypes(record.supportedInterfacesType);

            var connection = await connectionService.GetOrCreateConnectionAsync();
            var channel = await channelService.GetOrCreateChannelAsync(connection, channelPurpose);

            var exchangename = await exchangeService.GetOrCreateExchangeAsync(channel, record.GenericType);
            var queueName = await queueService.GetOrCreateQueuesAsync(channel, record.HandlerType, exchangename, queueType);

            await consumerService.GetOrCreateConsumerAsync(channel, queueName, services, record);
        }
    }
}
