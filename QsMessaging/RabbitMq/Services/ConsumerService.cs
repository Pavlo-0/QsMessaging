using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Text;

namespace QsMessaging.RabbitMq.Services
{
    internal class ConsumerService : IConsumerService
    {
        private readonly static ConcurrentBag<StoreConsumerRecord> storeConsumerRecords = new ConcurrentBag<StoreConsumerRecord>();

        public async Task CreateConsumer(IChannel channel, string queueName, object handlerInstance, HandlerService.HandlersStoreRecord record)
        {
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                byte[] body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                // Deserialize the message into an instance of genericHandlerType
                var modelInstance = System.Text.Json.JsonSerializer.Deserialize(message, record.GenericType);
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

            storeConsumerRecords.Add(new StoreConsumerRecord(channel, queueName, consumerTag, handlerInstance));
        }

        public IEnumerable<string> GetConsumersByChannel(IChannel channel)
        {
            return storeConsumerRecords.Where(c => c.Channel == channel).Select(c => c.ConsumerTag);
        }

        private record StoreConsumerRecord(IChannel Channel, string QueueName, string ConsumerTag, object HandlerInstance);
    }
}
