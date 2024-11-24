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

        public async Task CreateConsumer(
            IChannel channel,
            string queueName,
            object handlerInstance,
            HandlerService.HandlersStoreRecord record,
            IEnumerable<IQsMessagingConsumerErrorHandler> consumerErrorInstances)
        {
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {

                    byte[] body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    // Deserialize the message into an instance of genericHandlerType
                    var modelInstance = System.Text.Json.JsonSerializer.Deserialize(message, record.GenericType);

                    //TODO: replace on proper interface. Not just IQsMessageHandler. Add IQsEventHandler options. In case of diffirent names
                    var consumeMethod = record.HandlerType.GetMethod(nameof(IQsMessageHandler<object>.Consumer));

                    if (consumeMethod != null)
                    {
                        try
                        {
                            var resulttAsync = consumeMethod.Invoke(handlerInstance, new[] { modelInstance });
                        }
                        catch (Exception ex)
                        {
                            await ErrorAsync(
                        ex,
                        new ErrorConsumerDetail(
                            modelInstance,
                            body,
                            queueName,
                            record?.supportedInterfacesType?.FullName,
                            record?.ConcreteHandlerInterfaceType?.FullName,
                            record?.HandlerType?.FullName,
                            record?.GenericType?.FullName,
                            ErrorConsumerType.RecevingProblem),
                        consumerErrorInstances);
                        }
                    }
                }
                catch (Exception e)
                {
                    await ErrorAsync(
                        e,
                        new ErrorConsumerDetail(
                            null,
                            null,
                            queueName,
                            record?.supportedInterfacesType?.FullName,
                            record?.ConcreteHandlerInterfaceType?.FullName,
                            record?.HandlerType?.FullName,
                            record?.GenericType?.FullName,
                            ErrorConsumerType.RecevingProblem),
                        consumerErrorInstances);
                }
            };

            var consumerTag = await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
            storeConsumerRecords.Add(new StoreConsumerRecord(channel, queueName, consumerTag, handlerInstance));
        }

        public IEnumerable<string> GetConsumersByChannel(IChannel channel)
        {
            return storeConsumerRecords.Where(c => c.Channel == channel).Select(c => c.ConsumerTag);
        }

        private async Task ErrorAsync(Exception ex, ErrorConsumerDetail model, IEnumerable<IQsMessagingConsumerErrorHandler> consumerErrorInstances)
        {
            try
            {
                foreach (var errorInstance in consumerErrorInstances)
                {
                    await errorInstance.HandleErrorAsync(ex, model);
                }
            }
            catch
            {
                //Add critical log
            }
        }

        private record StoreConsumerRecord(IChannel Channel, string QueueName, string ConsumerTag, object HandlerInstance);
    }
}
