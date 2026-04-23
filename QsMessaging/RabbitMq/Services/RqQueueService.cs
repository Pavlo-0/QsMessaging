using Microsoft.Extensions.Logging;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Shared.Interface;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Collections.Concurrent;

namespace QsMessaging.RabbitMq.Services
{
    internal class RqQueueService(
        ILogger<RqQueueService> logger,
        IRqNameGenerator nameGenerator) : IRqQueueService
    {
        private static readonly ConcurrentBag<RqStoreQueueRecord> storeQueueRecords = new();
        private static readonly ConcurrentDictionary<Type, SemaphoreSlim> _locks = new();

        public async Task<string> GetOrCreateQueuesAsync(
            IChannel channel,
            Type TModel,
            string exchangeName,
            RqQueuePurpose queueType,
            CancellationToken cancellationToken = default)
        {
            var semaphore = _locks.GetOrAdd(TModel, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                logger.LogDebug("Attempting to declare queue");

                var queueName = nameGenerator.GetQueueNameFromType(TModel, queueType);
                var arguments = new Dictionary<string, object?>();
                var exclusive = false;
                var durable = true;
                var autoDelete = false;

                switch (queueType)
                {
                    case RqQueuePurpose.Permanent:
                        //arguments.Add("x-queue-mode", "lazy");
                        break;

                    case RqQueuePurpose.ConsumerTemporary:
                        exclusive = true;
                        durable = false;
                        autoDelete = true;
                        //arguments.Add("x-expires", 1);
                        break;
                    case RqQueuePurpose.InstanceTemporary:
                    case RqQueuePurpose.SingleTemporary:
                        autoDelete = true;
                        //arguments.Add("x-expires", 0);
                        //arguments.Add("x-queue-mode", "default");
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(queueType), queueType, "Unknown QueueType");
                }


                try
                {
                    await channel.QueueDeclareAsync(
                        queue: queueName,
                        durable: durable,
                        exclusive: exclusive,
                        autoDelete: autoDelete,
                        arguments: arguments,
                        cancellationToken: cancellationToken);

                    logger.LogDebug("Attempting to bind queue");

                    await channel.QueueBindAsync(
                        queue: queueName,
                        exchange: exchangeName,
                        routingKey: string.Empty,
                        arguments: null,
                        cancellationToken: cancellationToken);
                }
                catch (OperationInterruptedException oie)
                {
                    logger.LogError(oie, $"Failed to declare the {queueName} queue. Try to delete manually.");
                    throw oie;
                    /*
                    logger.LogTrace("This queue already exists. For permanent, instance and single queues, this exception can be ignored.");

                    if (queueType == RqQueuePurpose.ConsumerTemporary)
                    {
                        logger.LogError(oie, "Failed to declare the temporary queue, which should be single.");
                        throw;
                    }*/
                }

                if (!storeQueueRecords.Any(q =>
                    q.Channel == channel &&
                    q.TModel == TModel &&
                    q.ExchangeName == exchangeName &&
                    q.QueueName == queueName))
                {
                    storeQueueRecords.Add(new RqStoreQueueRecord(channel, TModel, exchangeName, queueName));
                }

                return queueName;
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}