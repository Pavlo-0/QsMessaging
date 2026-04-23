using Microsoft.Extensions.Logging;
using QsMessaging.Public;
using QsMessaging.RabbitMq.Services.Interfaces;
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

            var queueNames = await managementService.GetQueueNamesAsync(cancellationToken);
            var exchangeNames = (await managementService.GetExchangeNamesAsync(cancellationToken))
                .Where(CanDeleteExchange)
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
                queueNames.Count,
                exchangeNames.Length,
                configuration.RabbitMQ.VirtualHost);
        }

        private static bool CanDeleteExchange(string exchangeName)
        {
            return !string.IsNullOrWhiteSpace(exchangeName)
                && !exchangeName.StartsWith("amq.", StringComparison.OrdinalIgnoreCase);
        }
    }
}
