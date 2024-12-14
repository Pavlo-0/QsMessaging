using QsMessaging.RabbitMq.Models;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IConsumerService
    {
        Task<string> GetOrCreateConsumerAsync(
            IChannel channel, 
            string queueName,
            HandlersStoreRecord record);

        IEnumerable<string> GetConsumersByChannel(IChannel channel);
    }
}
