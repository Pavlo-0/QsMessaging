using QsMessaging.Public.Handler;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IConsumerService
    {
        Task CreateConsumer(
            IChannel channel, 
            string queueName,
            IServiceProvider serviceProvider,
            //object handlerInstance, 
            HandlerService.HandlersStoreRecord record
            //IEnumerable<IQsMessagingConsumerErrorHandler> consumerErrorInstances
            );

        IEnumerable<string> GetConsumersByChannel(IChannel channel);
    }
}
