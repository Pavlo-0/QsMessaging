using Microsoft.Extensions.Logging;
using QsMessaging.Public;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Shared;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services;
using QsMessaging.Shared.Services.Interfaces;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace QsMessaging.RabbitMq
{
    internal sealed class RqTransportCleaner(
        ILogger<RqTransportCleaner> logger,
        IRqConnectionService connectionService,
        IRqNameGenerator nameGenerator,
        IHandlerService handlerService) : IQsMessagingTransportCleaner
    {
        public async Task CleanUp(CancellationToken cancellationToken = default)
        {
            if (MessageHandlerExecutionContext.IsInsideHandler)
            {
                throw new InvalidOperationException("RabbitMQ transport cleanup cannot run inside a message handler.");
            }

            var handlers = handlerService.GetHandlers().ToArray();
            var queueNames = handlers
                .Select(GetQueueName)
                .Where(queueName => !string.IsNullOrWhiteSpace(queueName))
                .Select(queueName => queueName!)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var exchangeNames = handlers
                .Select(record => nameGenerator.GetExchangeNameFromType(
                    record.GenericType,
                    HardConfiguration.GetExchangePurpose(record.supportedInterfacesType)))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (queueNames.Length == 0 && exchangeNames.Length == 0)
            {
                logger.LogInformation("RabbitMQ cleanup skipped because no QsMessaging entities were discovered.");
                return;
            }

            await connectionService.GetOrCreateConnectionAsync(cancellationToken);
            var connection = connectionService.GetConnection()
                ?? throw new InvalidOperationException("RabbitMQ connection was not created for cleanup.");

            try
            {
                foreach (var queueName in queueNames)
                {
                    await DeleteQueueAsync(connection, queueName, cancellationToken);
                }

                foreach (var exchangeName in exchangeNames)
                {
                    await DeleteExchangeAsync(connection, exchangeName, cancellationToken);
                }
            }
            finally
            {
                try
                {
                    await connectionService.CloseAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to close RabbitMQ cleanup connection cleanly.");
                }
            }
        }

        private string? GetQueueName(HandlersStoreRecord record)
        {
            var queuePurpose = HardConfiguration.GetQueuePurpose(record.supportedInterfacesType);

            return queuePurpose switch
            {
                RqQueuePurpose.Permanent or
                RqQueuePurpose.InstanceTemporary or
                RqQueuePurpose.SingleTemporary => nameGenerator.GetQueueNameFromType(record.GenericType, queuePurpose),
                RqQueuePurpose.ConsumerTemporary => null,
                _ => throw new ArgumentOutOfRangeException(nameof(queuePurpose), queuePurpose, "Unsupported RabbitMQ queue purpose.")
            };
        }

        private async Task DeleteQueueAsync(IConnection connection, string queueName, CancellationToken cancellationToken)
        {
            await using var channel = await connection.CreateChannelAsync(options: null, cancellationToken);

            try
            {
                await channel.QueueDeleteAsync(
                    queue: queueName,
                    ifUnused: false,
                    ifEmpty: false,
                    noWait: false,
                    cancellationToken: cancellationToken);

                logger.LogInformation("RabbitMQ queue {QueueName} deleted.", queueName);
            }
            catch (OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == 404)
            {
                logger.LogDebug(ex, "RabbitMQ queue {QueueName} was already removed.", queueName);
            }
        }

        private async Task DeleteExchangeAsync(IConnection connection, string exchangeName, CancellationToken cancellationToken)
        {
            await using var channel = await connection.CreateChannelAsync(options: null, cancellationToken);

            try
            {
                await channel.ExchangeDeleteAsync(
                    exchange: exchangeName,
                    ifUnused: false,
                    noWait: false,
                    cancellationToken: cancellationToken);

                logger.LogInformation("RabbitMQ exchange {ExchangeName} deleted.", exchangeName);
            }
            catch (OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == 404)
            {
                logger.LogDebug(ex, "RabbitMQ exchange {ExchangeName} was already removed.", exchangeName);
            }
        }
    }
}
