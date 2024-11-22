using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using QsMessaging.RabbitMq.Interface;
using QsMessaging.Public.Handler;
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
        /*
        public async Task SubscribeMessageHandlerAsync(Type interfaceType, Type handlerType, Type genericHandlerType)
        {
            await SubscribeHandlerAsync(interfaceType, handlerType, genericHandlerType, QueueType.Permanent);
        }

        public async Task SubscribeEventHandlerAsync(Type interfaceType, Type handlerType, Type genericHandlerType)
        {
            await SubscribeHandlerAsync(interfaceType, handlerType, genericHandlerType, QueueType.Temporary);
        }*/

        //public async Task SubscribeHandlerAsync(Type ConcreteHandlerInterfaceType, Type HandlerType, Type GenericType, QueueType queueType

        //public async Task SubscribeHandlerAsync(Type interfaceType, Type handlerType, Type genericHandlerType, QueueType queueType)

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
            /*
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                byte[] body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                // Deserialize the message into an instance of genericHandlerType
                var modelInstance = System.Text.Json.JsonSerializer.Deserialize(message, record.GenericType);

                var handlerInstance = services.GetService(record.ConcreteHandlerInterfaceType);

                var consumeMethod = record.HandlerType.GetMethod(nameof(IQsMessageHandler<object>.Consumer));
                if (consumeMethod != null)
                {
                    var resulttAsync = consumeMethod.Invoke(handlerInstance, new[] { modelInstance });

                    if (resulttAsync is Task<bool> resultTask)
                    {
                        var result = await resultTask;
                    }
                }
            };

            var consumerTag = await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
            */
            /*
            consumer.UnregisteredAsync += async (sender, args) =>
            {
                //consumer.Received -= handler; // e.g. AsyncEventingBasicConsumer
                //consumer.Channel.cl .Close(); // IModel
                await Task.CompletedTask;
            };

            consumer.ShutdownAsync += async (sender, args) =>
            {
                //consumer.Received -= handler; // e.g. AsyncEventingBasicConsumer
                //consumer.Channel.cl .Close(); // IModel
                await Task.CompletedTask;
            };*/


        }
    }
}
