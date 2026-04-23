using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Shared;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services;
using QsMessaging.Shared.Services.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Text;

namespace QsMessaging.RabbitMq.Services
{
    internal class RqConsumerService(
        ILogger<RqConsumerService> logger,
        IConsumerService consumerService) : IRqConsumerService
    {
        private readonly static ConcurrentBag<RqStoreConsumerRecord> storeConsumerRecords = new ConcurrentBag<RqStoreConsumerRecord>();

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
                await using var _ = MessageHandlerExecutionContext.Enter();

                try
                {
                    await consumerService.UniversalConsumer(
                        data: ea.Body.ToArray(),
                        record: record,
                        correlationId: ea.BasicProperties.CorrelationId,
                        replyTo: ea.BasicProperties.ReplyTo ?? string.Empty,
                        name: queueName,
                        cancellationToken: CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while processing the message in HandleMessageAsync.");
                    //await TryNegativeAckAsync(channel, ea.DeliveryTag);
                }
                finally
                {
                    await channel.BasicAckAsync(
                        deliveryTag: ea.DeliveryTag,
                        multiple: false,
                        cancellationToken: CancellationToken.None);
                }

            };

            logger.LogTrace("Register basic consumer.");
            var consumerTag = await channel.BasicConsumeAsync(queueName, autoAck: false, consumer: consumer);
            storeConsumerRecords.Add(new RqStoreConsumerRecord(channel, queueName, consumerTag));
            return consumerTag;
        }

        public IEnumerable<string> GetConsumersByChannel(IChannel channel)
        {
            return storeConsumerRecords.Where(c => c.Channel == channel).Select(c => c.ConsumerTag);
        }

        private async Task TryNegativeAckAsync(IChannel channel, ulong deliveryTag)
        {
            try
            {
                if (!channel.IsOpen)
                {
                    logger.LogWarning("RabbitMQ channel is already closed; skipping nack for delivery tag {DeliveryTag}.", deliveryTag);
                    return;
                }

                await channel.BasicNackAsync(
                    deliveryTag: deliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken: CancellationToken.None);
            }
            catch (Exception nackException)
            {
                logger.LogWarning(nackException, "Failed to nack RabbitMQ delivery tag {DeliveryTag}.", deliveryTag);
            }
        }
    }
}
