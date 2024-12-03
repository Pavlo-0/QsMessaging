using Microsoft.Extensions.DependencyInjection;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Text;

namespace QsMessaging.RabbitMq.Services
{
    internal class ConsumerService(ISender sender) : IConsumerService
    {
        private readonly static ConcurrentBag<StoreConsumerRecord> storeConsumerRecords = new ConcurrentBag<StoreConsumerRecord>();

        public async Task<string> GetOrCreateConsumerAsync(
            IChannel channel,
            string queueName,
            IServiceProvider serviceProvider,
            HandlersStoreRecord record)
        {
            if (storeConsumerRecords.Any(consumer => consumer.QueueName == queueName && consumer.Channel == channel))
            {
                //Consumer already exists
                return storeConsumerRecords.First(consumer => consumer.QueueName == queueName && consumer.Channel == channel).ConsumerTag;
            }

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                List<IQsMessagingConsumerErrorHandler> consumerErrorInstances = new List<IQsMessagingConsumerErrorHandler>();
                try
                {
                    try
                    {
                        consumerErrorInstances.AddRange(serviceProvider.GetServices<IQsMessagingConsumerErrorHandler>());
                    }
                    catch
                    {
                        //TODO: Add loging  for extreame case
                    }

                    var correlationId = ea.BasicProperties.CorrelationId ?? string.Empty;
                    byte[] body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    // Deserialize the message into an instance of genericHandlerType
                    var modelInstance = System.Text.Json.JsonSerializer.Deserialize(message, record.GenericType);

                    //TODO: replace on proper interface. Not just IQsMessageHandler. Add IQsEventHandler options. In case of diffirent names
                    var consumeMethod = record.HandlerType.GetMethod(nameof(IQsMessageHandler<object>.Consumer)) ?? throw new NullReferenceException("Ca'nt find methof for consume model");
                    var handlerInstance = serviceProvider.GetService(record.ConcreteHandlerInterfaceType) ?? throw new Exception($"Handler instance for {record.ConcreteHandlerInterfaceType} is null.");

                    try
                    {
                        switch (HardConfiguration.GetConsumerByInterfaceTypes(record.supportedInterfacesType))
                        {
                            case ConsumerPurpose.MessageEventConsumer:
                                var resultAsync = consumeMethod.Invoke(handlerInstance, new[] { modelInstance });
                                break;

                            case ConsumerPurpose.RRRequestConsumer:
                                var resultModelAsync = consumeMethod.Invoke(handlerInstance, new[] { modelInstance });

                                if (resultModelAsync is Task task)
                                {
                                    await task;
                                    var result = task.GetType().GetProperty("Result")?.GetValue(task)
                                        ?? throw new NullReferenceException("RequestResponseHandler have to return result");

                                    await sender.SendMessageCorrelationAsync(result, correlationId);
                                }
                                else
                                {
                                    throw new NullReferenceException("RequestResponseHandler have to return Task<result>");
                                }
                                break;

                            case ConsumerPurpose.RRResponseConsumer:
                                var resulttAsync = consumeMethod.Invoke(handlerInstance, new[] { modelInstance, correlationId });
                                break;

                            default:
                                //TODO: add warning that consumer not found
                                break;
                        }
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
            storeConsumerRecords.Add(new StoreConsumerRecord(channel, queueName, consumerTag));
            return consumerTag;
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
    }
}
