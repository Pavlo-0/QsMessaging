using QsMessaging.Shared.Models;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IRqConsumerService
    {
        Task<string> GetOrCreateConsumerAsync(
            IChannel channel, 
            string queueName,
            HandlersStoreRecord record,
            CancellationToken cancellationToken = default);

        IEnumerable<string> GetConsumersByChannel(IChannel channel);

        Task CloseAsync(CancellationToken cancellationToken = default);
    }
}
