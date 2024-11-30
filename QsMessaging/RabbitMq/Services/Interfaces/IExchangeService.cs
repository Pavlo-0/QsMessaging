using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IExchangeService
    {
        Task<string> GetOrCreateExchange(IChannel channel, Type TModel);
    }
}