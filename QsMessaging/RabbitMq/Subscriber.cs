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
            var queueType = HardConfiguration.GetQueueByInterfaceTypes(record.supportedInterfacesType);
            var connection = await connectionService.GetOrCreateConnectionAsync();
            var channel = await channelService.GetOrCreateChannelAsync(connection,
              queueType == QueueType.Permanent
              ? ChannelService.ChannelPurpose.QueuePermanent
              : ChannelService.ChannelPurpose.QueueTemporary
                );

            var exchangename = await exchangeService.CreateExchange(channel, record.GenericType);
            var queueName = await queueService.CreateQueues(channel, record.HandlerType, exchangename, queueType);
            var handlerInstance = services.GetService(record.ConcreteHandlerInterfaceType);
            if (handlerInstance is null)
            {
                throw new Exception($"Handler instance for {record.ConcreteHandlerInterfaceType} is null.");
            }

            await consumerService.CreateConsumer(channel, queueName, handlerInstance, record, consumerErrorInstances);
        }
    }
}
