using QsMessaging.RabbitMq.Models.Enums;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services.Interfaces
{
    internal interface IRqExchangeService
    {
        Task<string> GetOrCreateExchangeAsync(IChannel channel, Type TModel, RqExchangePurpose purpose);
    }
}