using Microsoft.Extensions.DependencyInjection;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
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
            HandlerService.HandlersStoreRecord record
            )
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

                    byte[] body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    // Deserialize the message into an instance of genericHandlerType
                    var modelInstance = System.Text.Json.JsonSerializer.Deserialize(message, record.GenericType);

                    //TODO: replace on proper interface. Not just IQsMessageHandler. Add IQsEventHandler options. In case of diffirent names
                    var consumeMethod = record.HandlerType.GetMethod(nameof(IQsMessageHandler<object>.Consumer));
                    var handlerInstance = serviceProvider.GetService(record.ConcreteHandlerInterfaceType);
                    if (handlerInstance is null)
                    {
                        throw new Exception($"Handler instance for {record.ConcreteHandlerInterfaceType} is null.");
                    }

                    var correlationId = ea.BasicProperties.CorrelationId ?? string.Empty;

                    if (consumeMethod != null)
                    {
                        try
                        {
                            switch (HardConfiguration.GetConsumerByInterfaceTypes(record.supportedInterfacesType))
                            {
                                case ConsumerType.MessageEventConsumer:
                                    var resultAsync = consumeMethod.Invoke(handlerInstance, new[] { modelInstance });
                                    break;

                                case ConsumerType.RRRequestConsumer:
                                    var resultModelAsync = consumeMethod.Invoke(handlerInstance, new[] { modelInstance });

                                    if (resultModelAsync is Task task) // Check if it is a Task
                                    {
                                        await task; // Blocks until the task completes
                                        var result = task.GetType().GetProperty("Result")?.GetValue(task);

                                        await sender.SendMessageCorrelationAsync(result, correlationId);
                                    }

                                        /*
                                        var resultTask = (Task<dynamic>)resultModelAsync;
                                        resultTask.Wait(); // Blocks until the task completes
                                        var result = resultTask.Result; // Gets the result of the Task<bool>
                                        await rabbitMqSender.SendMessageAsync(result);*/
                                        //We have send resultModelAsync back to the service.
                                        /*if (resultModelAsync is Task resultTask && (record.GenericType2 is not null))
                                        {



                                            await rabbitMqSender.SendMessageAsync(result);
                                        }
                                        else
                                        {
                                            throw new Exception("Doesn't have type of object for return");
                                        }*/
                                        break;

                                case ConsumerType.RRResponseConsumer:
                                    // Get the CorrelationId (if set)
                                    //var correlationId = ea.BasicProperties.CorrelationId ?? string.Empty;
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

        private record StoreConsumerRecord(IChannel Channel, string QueueName, string ConsumerTag);
    }

    public enum ConsumerType
    {
        MessageEventConsumer,
        RRRequestConsumer,
        RRResponseConsumer,
    }
}
