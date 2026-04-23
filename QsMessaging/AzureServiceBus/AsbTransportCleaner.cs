using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Models.Enums;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;
using QsMessaging.Shared;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services.Interfaces;

namespace QsMessaging.AzureServiceBus
{
    internal sealed class AsbTransportCleaner(
        ILogger<AsbTransportCleaner> logger,
        IAsbConnectionService connectionService,
        IAsbNameGeneratorService nameGenerator,
        IHandlerService handlerService) : IQsMessagingTransportCleaner
    {
        public async Task CleanUp(CancellationToken cancellationToken = default)
        {
            if (AsbMessageHandlerExecutionContext.IsInsideHandler)
            {
                throw new InvalidOperationException("Azure Service Bus transport cleanup cannot run inside a message handler.");
            }

            var handlers = handlerService.GetHandlers().ToArray();
            var subscriptions = handlers
                .Where(record => HardConfiguration.GetReciverPurpose(record.supportedInterfacesType) == AsbReciverPurpose.TopicSubscription)
                .Select(record => new SubscriptionCleanupTarget(
                    nameGenerator.GetAsbTopicNameFromType(record.GenericType),
                    nameGenerator.GetSubscriptionName(
                        record.GenericType,
                        HardConfiguration.GetSubscriptionPurpose(record.supportedInterfacesType))))
                .Distinct()
                .ToArray();
            var topicNames = subscriptions
                .Select(target => target.TopicName)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var queueNames = handlers
                .SelectMany(GetQueueNames)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (subscriptions.Length == 0 && topicNames.Length == 0 && queueNames.Length == 0)
            {
                logger.LogInformation("Azure Service Bus cleanup skipped because no QsMessaging entities were discovered.");
                return;
            }

            var administrationClient = await connectionService.GetOrCreateAdministrationClientAsync(cancellationToken);

            try
            {
                foreach (var subscription in subscriptions)
                {
                    await DeleteSubscriptionAsync(administrationClient, subscription, cancellationToken);
                }

                foreach (var topicName in topicNames)
                {
                    await DeleteTopicAsync(administrationClient, topicName, cancellationToken);
                }

                foreach (var queueName in queueNames)
                {
                    await DeleteQueueAsync(administrationClient, queueName, cancellationToken);
                }
            }
            finally
            {
                try
                {
                    await connectionService.CloseAdministrationClientAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to close Azure Service Bus administration client cleanly after cleanup.");
                }
            }
        }

        private IEnumerable<string> GetQueueNames(HandlersStoreRecord record)
        {
            var receiverPurpose = HardConfiguration.GetReciverPurpose(record.supportedInterfacesType);

            switch (receiverPurpose)
            {
                case AsbReciverPurpose.QueueForRequest:
                    yield return nameGenerator.GetAsbQueueNameFromType(record.GenericType, AsbQueuePurpose.Request);
                    break;
                case AsbReciverPurpose.QueueForResponse:
                    yield return nameGenerator.GetAsbQueueNameFromType(record.GenericType, AsbQueuePurpose.Response);
                    break;
            }
        }

        private async Task DeleteSubscriptionAsync(
            ServiceBusAdministrationClient administrationClient,
            SubscriptionCleanupTarget target,
            CancellationToken cancellationToken)
        {
            if (!await administrationClient.SubscriptionExistsAsync(target.TopicName, target.SubscriptionName, cancellationToken))
            {
                return;
            }

            try
            {
                await administrationClient.DeleteSubscriptionAsync(target.TopicName, target.SubscriptionName, cancellationToken);
                logger.LogInformation(
                    "Azure Service Bus subscription {SubscriptionName} for topic {TopicName} deleted.",
                    target.SubscriptionName,
                    target.TopicName);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                logger.LogDebug(
                    ex,
                    "Azure Service Bus subscription {SubscriptionName} for topic {TopicName} was already removed.",
                    target.SubscriptionName,
                    target.TopicName);
            }
        }

        private async Task DeleteTopicAsync(
            ServiceBusAdministrationClient administrationClient,
            string topicName,
            CancellationToken cancellationToken)
        {
            if (!await administrationClient.TopicExistsAsync(topicName, cancellationToken))
            {
                return;
            }

            try
            {
                await administrationClient.DeleteTopicAsync(topicName, cancellationToken);
                logger.LogInformation("Azure Service Bus topic {TopicName} deleted.", topicName);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                logger.LogDebug(ex, "Azure Service Bus topic {TopicName} was already removed.", topicName);
            }
        }

        private async Task DeleteQueueAsync(
            ServiceBusAdministrationClient administrationClient,
            string queueName,
            CancellationToken cancellationToken)
        {
            if (!await administrationClient.QueueExistsAsync(queueName, cancellationToken))
            {
                return;
            }

            try
            {
                await administrationClient.DeleteQueueAsync(queueName, cancellationToken);
                logger.LogInformation("Azure Service Bus queue {QueueName} deleted.", queueName);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                logger.LogDebug(ex, "Azure Service Bus queue {QueueName} was already removed.", queueName);
            }
        }

        private sealed record SubscriptionCleanupTarget(string TopicName, string SubscriptionName);
    }
}
