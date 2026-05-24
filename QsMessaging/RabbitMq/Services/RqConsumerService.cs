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
            HandlersStoreRecord record,
            CancellationToken cancellationToken = default)
        {
            if (storeConsumerRecords.Any(consumer => consumer.QueueName == queueName && consumer.Channel == channel))
            {
                logger.LogTrace("Consumer already exists");
                return storeConsumerRecords.First(consumer => consumer.QueueName == queueName && consumer.Channel == channel).ConsumerTag;
            }

            logger.LogDebug("Attempting to declare consumer");

            var consumer = new AsyncEventingBasicConsumer(channel);
            var consumerCancellation = new CancellationTokenSource();

            consumer.ShutdownAsync += (_, _) =>
            {
                CancelConsumer(consumerCancellation);
                return Task.CompletedTask;
            };

            consumer.UnregisteredAsync += (_, _) =>
            {
                CancelConsumer(consumerCancellation);
                return Task.CompletedTask;
            };

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
                        cancellationToken: consumerCancellation.Token);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while processing the message in HandleMessageAsync.");
                    //await TryNegativeAckAsync(channel, ea.DeliveryTag);
                }
                finally
                {
                    await RqChannelExecutor.ExecuteAsync(
                        channel,
                        async token => await channel.BasicAckAsync(
                            deliveryTag: ea.DeliveryTag,
                            multiple: false,
                            cancellationToken: token),
                        CancellationToken.None);
                }

            };

            logger.LogTrace("Register basic consumer.");
            try
            {
                var consumerTag = await RqChannelExecutor.ExecuteAsync(
                    channel,
                    async token => await channel.BasicConsumeAsync(
                            queueName,
                            autoAck: false,
                            consumer: consumer,
                            cancellationToken: token),
                    cancellationToken);
                storeConsumerRecords.Add(new RqStoreConsumerRecord(channel, queueName, consumerTag)
                {
                    CancellationTokenSource = consumerCancellation
                });
                return consumerTag;
            }
            catch
            {
                consumerCancellation.Dispose();
                throw;
            }
        }

        public IEnumerable<string> GetConsumersByChannel(IChannel channel)
        {
            return storeConsumerRecords.Where(c => c.Channel == channel).Select(c => c.ConsumerTag);
        }

        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            var consumerRecords = storeConsumerRecords.ToArray();
            if (consumerRecords.Length == 0)
            {
                return;
            }

            await Parallel.ForEachAsync(consumerRecords, async (consumerRecord, _) =>
            {
                CancelConsumer(consumerRecord.CancellationTokenSource);

                try
                {
                    if (consumerRecord.Channel.IsOpen)
                    {
                        await RqChannelExecutor.ExecuteAsync(
                            consumerRecord.Channel,
                            async token => await consumerRecord.Channel.BasicCancelAsync(
                                consumerRecord.ConsumerTag,
                                noWait: false,
                                cancellationToken: token),
                            cancellationToken);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    logger.LogDebug(ex, "RabbitMQ consumer cancellation was interrupted for {ConsumerTag}.", consumerRecord.ConsumerTag);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to cancel RabbitMQ consumer {ConsumerTag}.", consumerRecord.ConsumerTag);
                }
                finally
                {
                    consumerRecord.CancellationTokenSource.Dispose();
                }
            });

            storeConsumerRecords.Clear();
        }

        private static void CancelConsumer(CancellationTokenSource cancellationTokenSource)
        {
            try
            {
                cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
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

                await RqChannelExecutor.ExecuteAsync(
                    channel,
                    async token => await channel.BasicNackAsync(
                        deliveryTag: deliveryTag,
                        multiple: false,
                        requeue: true,
                        cancellationToken: token),
                    CancellationToken.None);
            }
            catch (Exception nackException)
            {
                logger.LogWarning(nackException, "Failed to nack RabbitMQ delivery tag {DeliveryTag}.", deliveryTag);
            }
        }
    }
}
