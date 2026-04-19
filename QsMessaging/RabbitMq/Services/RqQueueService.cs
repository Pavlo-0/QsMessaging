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
        private readonly static ConcurrentBag<RqStoreQueueRecord> storeQueueRecords = new ConcurrentBag<RqStoreQueueRecord>();

        public async Task<string> GetOrCreateQueuesAsync(IChannel channel, Type TModel, string exchangeName, RqQueuePurpose queueType)
        {
            logger.LogDebug("Attempting to declare queue");

            var queueName = nameGenerator.GetQueueNameFromType(TModel, queueType);
            var isAutoDelete = queueType == RqQueuePurpose.ConsumerTemporary ||
                queueType == RqQueuePurpose.InstanceTemporary ||
                queueType == RqQueuePurpose.SingleTemporary;

            logger.LogDebug("{Name}:{IsAutoDelete}", queueName, isAutoDelete);

            await channel.QueueDeclareAsync(
                queueName,
                durable: true,
                exclusive: false,
                autoDelete: isAutoDelete);

            var arguments = new Dictionary<string, object?>();

            switch (queueType)
            {
                case RqQueuePurpose.Permanent:
                    arguments.Add("x-queue-mode", "lazy");
                    break;
                case RqQueuePurpose.ConsumerTemporary:
                case RqQueuePurpose.InstanceTemporary:
                case RqQueuePurpose.SingleTemporary:
                    arguments.Add("x-expires", 0);
                    arguments.Add("x-queue-mode", "default");
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unknown QueueType");
            }

            try
            {
                logger.LogDebug("Attempting to bind queue");
                await channel.QueueBindAsync(queueName, exchangeName, string.Empty, arguments);
            }
            catch (OperationInterruptedException oie)
            {
                logger.LogTrace("This Queue already exist. For permanent queue (and instance and single), we can ignore this exception");
                //This Queue already exist. For permanent queue (and instance and single), we can ignore this exception
                if (queueType == RqQueuePurpose.ConsumerTemporary)
                {
                    logger.LogError(oie, "Failed to declare the temporary queue. Which should be singel");
                    throw;
                }
            }

            if (!storeQueueRecords.Where(q =>
                q.Channel == channel &&
                q.TModel == TModel &&
                q.ExchangeName == exchangeName &&
                q.QueueName == queueName).Any())
            {
                storeQueueRecords.Add(new RqStoreQueueRecord(channel, TModel, exchangeName, queueName));
            }

            return queueName;
        }
    }
}