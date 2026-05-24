using Microsoft.Extensions.Logging;
using QsMessaging.Public;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Shared;
using QsMessaging.Shared.Services;

namespace QsMessaging.RabbitMq
{
    internal sealed class RqTransportFullCleaner(
        ILogger<RqTransportFullCleaner> logger,
        IQsMessagingConfiguration configuration,
        IRqManagementService managementService) : IQsMessagingTransportFullCleaner
    {
        public async Task FullCleanUp(CancellationToken cancellationToken = default)
        {
            if (MessageHandlerExecutionContext.IsInsideHandler)
            {
                throw new InvalidOperationException("RabbitMQ full transport cleanup cannot run inside a message handler.");
            }

            LogTargetScope();

            var allowDangerousFullCleanup = configuration.AllowDangerousFullCleanup;
            var queueNames = (await managementService.GetQueueNamesAsync(cancellationToken))
                .Where(queueName => TransportFullCleanupNameFilter.CanDeleteRabbitMqQueue(queueName, allowDangerousFullCleanup))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var exchangeNames = (await managementService.GetExchangeNamesAsync(cancellationToken))
                .Where(exchangeName => TransportFullCleanupNameFilter.CanDeleteRabbitMqExchange(exchangeName, allowDangerousFullCleanup))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            foreach (var queueName in queueNames)
            {
                await managementService.DeleteQueueAsync(queueName, cancellationToken);
            }

            foreach (var exchangeName in exchangeNames)
            {
                await managementService.DeleteExchangeAsync(exchangeName, cancellationToken);
            }

            logger.LogInformation(
                "RabbitMQ full cleanup finished. Deleted {QueueCount} queues and {ExchangeCount} exchanges from virtual host {VirtualHost}.",
                queueNames.Length,
                exchangeNames.Length,
                configuration.RabbitMQ.VirtualHost);
        }

        private void LogTargetScope()
        {
            if (configuration.AllowDangerousFullCleanup)
            {
                logger.LogWarning(
                    "RabbitMQ dangerous full cleanup is enabled. Target scope: all queues and all non-reserved exchanges in virtual host {VirtualHost}.",
                    configuration.RabbitMQ.VirtualHost);
                return;
            }

            logger.LogInformation(
                "RabbitMQ full cleanup target scope: queues and exchanges with prefix {EntityPrefix} in virtual host {VirtualHost}.",
                TransportFullCleanupNameFilter.RabbitMqEntityPrefix,
                configuration.RabbitMQ.VirtualHost);
        }
    }
}
