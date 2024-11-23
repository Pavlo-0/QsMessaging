using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IExchangeService
    {
        Task<string> CreateExchange(IChannel channel, Type TModel);
    }
}