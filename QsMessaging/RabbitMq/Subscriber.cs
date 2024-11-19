using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;
using QsMessaging.RabbitMq.Interface;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.RabbitMq.Services;
using QsMessaging.Public;

namespace QsMessaging.RabbitMq
{
    internal class Subscriber(
        IServiceProvider services,
        IConnectionWorker connectionWorker,
        IExchangeGenerator exchangeGenerator,
        IQueueGenerator queueGenerator) : ISubscriber
    {

        public async Task SubscribeMessageHandlerAsync(Type interfaceType, Type handlerType, Type genericHandlerType)
        {
            await SubscribeHandlerAsync(interfaceType, handlerType, genericHandlerType, QueueType.Permanent);
        }

        public async Task SubscribeEventHandlerAsync(Type interfaceType, Type handlerType, Type genericHandlerType)
        {
            await SubscribeHandlerAsync(interfaceType, handlerType, genericHandlerType, QueueType.Temporary);
        }

        public async Task SubscribeHandlerAsync(Type interfaceType, Type handlerType, Type genericHandlerType, QueueType queueType)
        {
            var (connection, channel) = await connectionWorker.GetOrCreateConnectionAsync();

            var exchangename = await exchangeGenerator.CreateExchange(channel, genericHandlerType);
            var queueName = await queueGenerator.CreateQueues(channel, handlerType, exchangename, queueType);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                byte[] body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                // Deserialize the message into an instance of genericHandlerType
                var modelInstance = System.Text.Json.JsonSerializer.Deserialize(message, genericHandlerType);

                var handlerInstance = services.GetService(interfaceType);

                var consumeMethod = handlerType.GetMethod(nameof(IQsMessageHandler<object>.Consumer));
                if (consumeMethod != null)
                {
                    var resulttAsync = consumeMethod.Invoke(handlerInstance, new[] { modelInstance });

                    if (resulttAsync is Task<bool> resultTask)
                    {
                        var result = await resultTask;
                    }
                }
            };

            await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
        }
    }
}
