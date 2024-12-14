using Microsoft.Extensions.Logging;
using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Collections.Concurrent;

namespace QsMessaging.RabbitMq.Services
{
    internal class ExchangeService(ILogger<ExchangeService> logger, INameGenerator exchangeNameGenerator) : IExchangeService
    {
        private readonly static ConcurrentBag<StoreExchangeRecord> storeExchangeRecords = new ConcurrentBag<StoreExchangeRecord>();

        public async Task<string> GetOrCreateExchangeAsync(IChannel channel, Type TModel, ExchangePurpose purpose)
        {
            logger.LogDebug("Attempting to declare exchange");

            var name = exchangeNameGenerator.GetExchangeNameFromType(TModel);
            var isAutoDelete = purpose == ExchangePurpose.Permanent ? false : true;

            logger.LogDebug("{Name}:{IsAutoDelete}", name, isAutoDelete);

            try
            {
                await channel.ExchangeDeclareAsync(
                               exchange: name,
                               type: ExchangeType.Fanout,
                               durable: true,
                               autoDelete: isAutoDelete,
                               arguments: null);
            }
            catch (OperationInterruptedException ex) when (ex.ShutdownReason != null && ex.ShutdownReason.ReplyCode == 406)
            {
                // Exchange already exists but with different settings
                logger.LogError("Failed to declare the exchange. This exchange may already exist but with a different configuration. Please manually remove the exchange using RabbitMQ Management. The application will continue running, but proper message delivery cannot be guaranteed during service interruptions.");
            }

            storeExchangeRecords.Add(new StoreExchangeRecord(channel, TModel, name));

            return name;
        }
    }
}
