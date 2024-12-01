using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.RabbitMq.Services;
using Microsoft.Extensions.DependencyInjection;
using QsMessaging.Public.Handler;

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
            var consumerErrorInstanceHandlers = services.GetServices<IQsMessagingConsumerErrorHandler>();
             

            foreach (var record in handlerService.GetHandlers())
            {
                await SubscribeHandlerAsync(record, consumerErrorInstanceHandlers);
            }
        }

        public async Task SubscribeHandlerAsync(
            HandlerService.HandlersStoreRecord record, 
            IEnumerable<IQsMessagingConsumerErrorHandler> consumerErrorInstances)
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
