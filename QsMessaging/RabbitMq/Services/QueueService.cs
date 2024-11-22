using QsMessaging.RabbitMq.Interface;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Collections.Concurrent;

namespace QsMessaging.RabbitMq.Services
{
    internal class QueueService(INameGenerator nameGenerator) : IQueueService
    {
        private readonly static ConcurrentBag<StoreQueueRecord> storeQueueRecords = new ConcurrentBag<StoreQueueRecord>();

        public async Task<string> CreateQueues(IChannel channel, Type TModel, string exchangeName, QueueType queueType)
        {
            var queueName = nameGenerator.GetQueueNameFromType(TModel, queueType);

            await channel.QueueDeclareAsync(
                queueName,
                durable: true,
                //exclusive: false,
                exclusive: queueType == QueueType.Temporary,
                autoDelete: queueType == QueueType.Temporary);

            var arguments = new Dictionary<string, object?>();

            switch (queueType)
            {
                case QueueType.Permanent:
                    arguments.Add("x-queue-mode", "lazy");
                    break;
                case QueueType.Temporary:
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
                //This Queue already exist. For permanent queue, we can ignore this exception
                if (queueType == QueueType.Temporary)
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
}