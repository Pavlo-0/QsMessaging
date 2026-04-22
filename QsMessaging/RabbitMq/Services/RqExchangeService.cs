using Microsoft.Extensions.Logging;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Shared.Interface;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Collections.Concurrent;

namespace QsMessaging.RabbitMq.Services
{
    internal class RqExchangeService(
        ILogger<RqExchangeService> logger,
        IRqNameGenerator exchangeNameGenerator) : IRqExchangeService
    {
        private readonly static ConcurrentBag<RqStoreExchangeRecord> storeExchangeRecords = new ConcurrentBag<RqStoreExchangeRecord>();
        private static readonly ConcurrentDictionary<Type, SemaphoreSlim> _locks = new();

        public async Task<string> GetOrCreateExchangeAsync(IChannel channel, Type TModel, RqExchangePurpose purpose, CancellationToken cancellationToken = default)
        {
            var semaphore = _locks.GetOrAdd(TModel, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                logger.LogDebug("Attempting to declare exchange");

                var name = exchangeNameGenerator.GetExchangeNameFromType(TModel, purpose);
                var isAutoDelete = purpose == RqExchangePurpose.Permanent ? false : true;

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

                if (!storeExchangeRecords.Any(record => record.ExchangeName == name))
                {
                    storeExchangeRecords.Add(new RqStoreExchangeRecord(channel, TModel, name));
                }

                return name;
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
