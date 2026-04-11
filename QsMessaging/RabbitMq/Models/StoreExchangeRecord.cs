using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Models
{
    internal record StoreExchangeRecord(IChannel Channel, Type TModel, string ExchangeName);
}
