using Microsoft.Extensions.Logging;
using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Collections.Concurrent;
using System.Xml.Linq;

namespace QsMessaging.RabbitMq.Services
{
    internal class QueueService(ILogger<QueueService> logger, INameGenerator nameGenerator) : IQueueService
    {
        private readonly static ConcurrentBag<StoreQueueRecord> storeQueueRecords = new ConcurrentBag<StoreQueueRecord>();

        public async Task<string> GetOrCreateQueuesAsync(IChannel channel, Type TModel, string exchangeName, QueuePurpose queueType)
        {
            logger.LogDebug("Attempting to declare queue");

            var queueName = nameGenerator.GetQueueNameFromType(TModel, queueType);
            var isAutoDelete = queueType == QueuePurpose.ConsumerTemporary ||
                queueType == QueuePurpose.InstanceTemporary ||
                queueType == QueuePurpose.SingleTemporary;

            logger.LogDebug("{Name}:{IsAutoDelete}", queueName, isAutoDelete);

            await channel.QueueDeclareAsync(
                queueName,
                durable: true,
                exclusive: false,
                autoDelete: isAutoDelete);

            var arguments = new Dictionary<string, object?>();

            switch (queueType)
            {
                case QueuePurpose.Permanent:
                    arguments.Add("x-queue-mode", "lazy");
                    break;
                case QueuePurpose.ConsumerTemporary:
                case QueuePurpose.InstanceTemporary:
                case QueuePurpose.SingleTemporary:
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
                if (queueType == QueuePurpose.ConsumerTemporary)
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
                storeQueueRecords.Add(new StoreQueueRecord(channel, TModel, exchangeName, queueName));
            }

            return queueName;
        }
    }
}