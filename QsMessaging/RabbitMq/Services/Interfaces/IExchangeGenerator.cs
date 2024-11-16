using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IExchangeGenerator
    {
        Task<string> CreateExchange(IChannel channel, Type TModel);
    }
}