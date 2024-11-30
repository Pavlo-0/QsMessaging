using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;
using System.Collections.Concurrent;

namespace QsMessaging.RabbitMq.Services
{
    internal class ExchangeService(INameGenerator exchangeNameGenerator) : IExchangeService
    {
        private readonly static ConcurrentBag<StoreExchangeRecord> storeExchangeRecords = new ConcurrentBag<StoreExchangeRecord>();

        public async Task<string> GetOrCreateExchangeAsync(IChannel channel, Type TModel)
        {
            var name = exchangeNameGenerator.GetExchangeNameFromType(TModel);

            await channel.ExchangeDeclareAsync(
                           exchange: name,
                           type: ExchangeType.Fanout,
                           durable: true,
                           autoDelete: false,
                           arguments: null);

            storeExchangeRecords.Add(new StoreExchangeRecord(channel, TModel, name));

            return name;
        }

        private record StoreExchangeRecord(IChannel Channel, Type TModel, string ExchangeName);
    }
}
