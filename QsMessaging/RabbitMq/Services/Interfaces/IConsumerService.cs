using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IConsumerService
    {
        Task<string> GetOrCreateConsumerAsync(
            IChannel channel, 
            string queueName,
            IServiceProvider serviceProvider,
            HandlerService.HandlersStoreRecord record);

        IEnumerable<string> GetConsumersByChannel(IChannel channel);
    }
}
