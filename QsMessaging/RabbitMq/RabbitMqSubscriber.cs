using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QsMessaging.RabbitMq.Interface;
using QsMessaging.Public.Handler;
using Microsoft.Extensions.DependencyInjection;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.RabbitMq.Services;

namespace QsMessaging.RabbitMq
{
    internal class RabbitMqSubscriber(
        IServiceProvider services,
        IRabbitMqConnectionStorage connectionStorage,
        IExchangeNameGenerator exchangeNameGenerator,
        IQueuesGenerator queuesGenerator) : IRabbitMqSubscriber
    {
        public async Task SubscribeAsync(Type interfaceType, Type handlerType, Type genericHandlerType)
        {
            var (connection, channel) = await connectionStorage.GetConnectionAsync();

            var exchangeName = exchangeNameGenerator.GetExchangeNameFromType(genericHandlerType);
            var queueName = exchangeNameGenerator.GetQueueNameFromType(handlerType);

            await queuesGenerator.CreateQueues(channel, exchangeName);

            // declare a server-named queue
            await channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: string.Empty);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                byte[] body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Console.WriteLine($" [x] {message}");

                // Deserialize the message into an instance of genericHandlerType
                var modelInstance = System.Text.Json.JsonSerializer.Deserialize(message, genericHandlerType);

                Console.WriteLine($" [x] {modelInstance}");

                var handlerInstance = services.GetService(interfaceType);

                // Call the Consume method of IQsMessageHandler<TModel> using reflection
                var consumeMethod = handlerType.GetMethod(nameof(IQsMessageHandler<object>.Consumer));
                if (consumeMethod != null)
                {
                    var result = await (Task<bool>)consumeMethod.Invoke(handlerInstance, new[] { modelInstance });
                    Console.WriteLine($" [x] {result}");
                }
            };

            await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
        }
    }
}
