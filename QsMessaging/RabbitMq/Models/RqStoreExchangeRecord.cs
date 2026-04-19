using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Models
{
    internal record RqStoreExchangeRecord(IChannel Channel, Type TModel, string ExchangeName);
}
