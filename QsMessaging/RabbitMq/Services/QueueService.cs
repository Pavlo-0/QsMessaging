using QsMessaging.RabbitMq.Interface;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Collections.Concurrent;

namespace QsMessaging.RabbitMq.Services
{
    internal class QueueService(INameGenerator nameGenerator) : IQueueService
    {
        private readonly static ConcurrentBag<StoreQueueRecord> storeQueueRecords = new ConcurrentBag<StoreQueueRecord>();

        public async Task<string> GetOrCreateQueuesAsync(IChannel channel, Type TModel, string exchangeName, QueueType queueType)
        {
            var queueName = nameGenerator.GetQueueNameFromType(TModel, queueType);

            await channel.QueueDeclareAsync(
                queueName,
                durable: true,
                exclusive: false,
                //exclusive: queueType == QueueType.Temporary,
                autoDelete: queueType == QueueType.ConsumerTemporary);

            var arguments = new Dictionary<string, object?>();

            switch (queueType)
            {
                case QueueType.Permanent:
                    arguments.Add("x-queue-mode", "lazy");
                    break;
                case QueueType.ConsumerTemporary:
                case QueueType.InstanceTemporary:
                case QueueType.SingleTemporary:
                    arguments.Add("x-expires", 0);
                    arguments.Add("x-queue-mode", "default");
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Unknown QueueType");
            }

            try
            {
                await channel.QueueBindAsync(queueName, exchangeName, string.Empty, arguments);
            }
            catch (OperationInterruptedException)
            {
                //This Queue already exist. For permanent queue (and instance and single), we can ignore this exception
                if (queueType == QueueType.ConsumerTemporary)
                {
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

        private record StoreQueueRecord(IChannel Channel, Type TModel, string ExchangeName, string QueueName);
    }


    internal enum QueueType
    {
        /// <summary>
        /// Single queue. Permanent.
        /// Message would be distributed to only one consumer which can consume message or wait when consumer would be ready.
        /// </summary>
        Permanent,

        /// <summary>
        /// Per Consumer queue. Temporary.
        /// Message would be distributed to all consumers.
        /// </summary>
        ConsumerTemporary,

        /// <summary>
        /// Per instance queue. Temporary.
        /// Every instance has own queue. Queue will be deleted after instance disconnect.
        /// Message would be distributed for one consumer in every instance.
        /// </summary>
        InstanceTemporary,

        /// <summary>
        /// Single queue. Temporary.
        /// Every instance consume one queue. Queue will be deleted after last consumer disconnect.
        /// Message would be distributed for one consumer.
        /// </summary>
        SingleTemporary
    }
}