using QsMessaging.RabbitMq.Interface;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace QsMessaging.RabbitMq.Services
{
    internal class QueueGenerator(INameGenerator nameGenerator) : IQueueGenerator
    {
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
            return queueName;
        }
    }
}
