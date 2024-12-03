using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Collections.Concurrent;

namespace QsMessaging.RabbitMq.Services
{
    internal class QueueService(INameGenerator nameGenerator) : IQueueService
    {
        private readonly static ConcurrentBag<StoreQueueRecord> storeQueueRecords = new ConcurrentBag<StoreQueueRecord>();

        public async Task<string> GetOrCreateQueuesAsync(IChannel channel, Type TModel, string exchangeName, QueuePurpose queueType)
        {
            var queueName = nameGenerator.GetQueueNameFromType(TModel, queueType);

            var isAutoDelete = queueType == QueuePurpose.ConsumerTemporary ||
                queueType == QueuePurpose.InstanceTemporary ||
                queueType == QueuePurpose.SingleTemporary;

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
                await channel.QueueBindAsync(queueName, exchangeName, string.Empty, arguments);
            }
            catch (OperationInterruptedException)
            {
                //This Queue already exist. For permanent queue (and instance and single), we can ignore this exception
                if (queueType == QueuePurpose.ConsumerTemporary)
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
    }
}