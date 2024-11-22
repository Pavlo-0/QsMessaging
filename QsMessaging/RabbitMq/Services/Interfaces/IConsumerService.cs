using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IConsumerService
    {
        Task CreateConsumer(IChannel channel, string queueName, object handlerInstance, HandlerService.HandlersStoreRecord record);
    }
}
