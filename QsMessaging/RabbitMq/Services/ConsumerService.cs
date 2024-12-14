using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    internal class ConsumerService(ILogger<ConsumerService> logger, ISender sender, IServiceProvider services) : IConsumerService
    {
        private readonly static ConcurrentBag<StoreConsumerRecord> storeConsumerRecords = new ConcurrentBag<StoreConsumerRecord>();

        public async Task<string> GetOrCreateConsumerAsync(
            IChannel channel,
            string queueName,
            HandlersStoreRecord record)
        {
            if (storeConsumerRecords.Any(consumer => consumer.QueueName == queueName && consumer.Channel == channel))
            {
                logger.LogTrace("Consumer already exists");
                return storeConsumerRecords.First(consumer => consumer.QueueName == queueName && consumer.Channel == channel).ConsumerTag;
            }

            logger.LogDebug("Attempting to declare consumer");

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    logger.LogInformation("Received message");

                    var correlationId = ea.BasicProperties.CorrelationId ?? string.Empty;
                    byte[] body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    // Deserialize the message into an instance of genericHandlerType
                    var modelInstance = System.Text.Json.JsonSerializer.Deserialize(message, record.GenericType);

                    //TODO: replace on proper interface. Not just IQsMessageHandler. Add IQsEventHandler options. In case of diffirent names
                    var consumeMethod = record.HandlerType.GetMethod(nameof(IQsMessageHandler<object>.Consumer)) ?? throw new NullReferenceException("Ca'nt find methof for consume model");
                    var handlerInstance = services.GetService(record.ConcreteHandlerInterfaceType) ?? throw new Exception($"Handler instance for {record.ConcreteHandlerInterfaceType} is null.");

                    logger.LogDebug("{CorrelationId}:{Type}", correlationId, record.GenericType.FullName);

                    try
                    {
                        switch (HardConfiguration.GetConsumerPurpose(record.supportedInterfacesType))
                        {
                            case ConsumerPurpose.MessageEventConsumer:
                                logger.LogTrace("MessageEventConsumer");
                                var resultAsync = consumeMethod.Invoke(handlerInstance, new[] { modelInstance });
                                break;

                            case ConsumerPurpose.RRRequestConsumer:
                                logger.LogTrace("RRRequestConsumer");
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
                                    logger.LogError("No Task<T> result was found. This is unexpected and may indicate an internal issue. Verify the RequestResponseHandler implementation.");
                                    throw new NullReferenceException("No Task<T> result was found. This is unexpected and may indicate an internal issue. Verify the RequestResponseHandler implementation.");
                                }
                                break;

                            case ConsumerPurpose.RRResponseConsumer:
                                logger.LogTrace("RRResponseConsumer");
                                var resulttAsync = consumeMethod.Invoke(handlerInstance, new[] { modelInstance, correlationId });
                                break;

                            default:
                                logger.LogError("No consumer found for the specified operation. This is unexpected and may indicate a internal issue.");
                                throw new NullReferenceException("No consumer found for the specified operation. This is unexpected and may indicate a internal issue.");
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
                                ErrorConsumerType.RecevingProblem));
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
                            ErrorConsumerType.RecevingProblem));
                }
            };

            logger.LogTrace("Register basic consumer.");
            var consumerTag = await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer);
            storeConsumerRecords.Add(new StoreConsumerRecord(channel, queueName, consumerTag));
            return consumerTag;
        }

        public IEnumerable<string> GetConsumersByChannel(IChannel channel)
        {
            return storeConsumerRecords.Where(c => c.Channel == channel).Select(c => c.ConsumerTag);
        }

        private async Task ErrorAsync(Exception ex, ErrorConsumerDetail model)
        {
            try
            {
                var consumerErrorInstances = services.GetServices<IQsMessagingConsumerErrorHandler>();

                foreach (var errorInstance in consumerErrorInstances)
                {
                    await errorInstance.HandleErrorAsync(ex, model);
                }
            }
            catch (Exception e)
            {
                logger.LogCritical(e, "An exception occurred while handling an error. Review the error handler logic and ensure proper error handling mechanisms are in place.");
            }
        }
    }
}
