using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IQueuesGenerator
    {
        Task CreateQueues(IChannel channel, string exchangeName);
    }
}
