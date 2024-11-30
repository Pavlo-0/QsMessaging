using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IExchangeService
    {
        Task<string> GetOrCreateExchangeAsync(IChannel channel, Type TModel);
    }
}