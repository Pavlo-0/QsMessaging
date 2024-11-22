using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.RabbitMq.Services;

namespace QsMessaging.RabbitMq
{
    internal class Subscriber(
        IServiceProvider services,
        IConnectionService connectionGenerator,
        IChannelService channelGenerator,
        IExchangeService exchangeGenerator,
        IQueueService queueGenerator,
        IHandlerService handlerGenerator,
        IConsumerService consumerGenerator) : ISubscriber
    {

        public async Task Subscribe()
        {
            foreach (var record in handlerGenerator.GetHandlers())
            {
                await SubscribeHandlerAsync(record);

            }
        }

        public async Task SubscribeHandlerAsync(HandlerService.HandlersStoreRecord record)
        {
            var queueType = HardConfiguration.GetQueueByInterfaceTypes(record.supportedInterfacesType);
            var connection = await connectionGenerator.GetOrCreateConnectionAsync();
            var channel = await channelGenerator.GetOrCreateChannelAsync(connection,
              queueType == QueueType.Permanent
              ? ChannelService.ChannelPurpose.QueuePermanent
              : ChannelService.ChannelPurpose.QueueTemporary
                );

            var exchangename = await exchangeGenerator.CreateExchange(channel, record.GenericType);
            var queueName = await queueGenerator.CreateQueues(channel, record.HandlerType, exchangename, queueType);
            var handlerInstance = services.GetService(record.ConcreteHandlerInterfaceType);

            if (handlerInstance is null)
            {
                throw new Exception($"Handler instance for {record.ConcreteHandlerInterfaceType} is null.");
            }

            await consumerGenerator.CreateConsumer(channel, queueName, handlerInstance, record);
        }
    }
}
