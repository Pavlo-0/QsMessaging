using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services
{
    internal record StoreExchangeRecord(IChannel Channel, Type TModel, string ExchangeName);
}
