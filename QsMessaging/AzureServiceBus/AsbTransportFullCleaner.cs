using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services;
using QsMessaging.Public;
using QsMessaging.Shared;

namespace QsMessaging.AzureServiceBus
{
    internal sealed class AsbTransportFullCleaner(
        ILogger<AsbTransportFullCleaner> logger,
        IQsMessagingConfiguration configuration,
        Services.Interfaces.IAsbConnectionService connectionService) : IQsMessagingTransportFullCleaner
    {
        public async Task FullCleanUp(CancellationToken cancellationToken = default)
        {
            if (AsbMessageHandlerExecutionContext.IsInsideHandler)
            {
                throw new InvalidOperationException("Azure Service Bus full transport cleanup cannot run inside a message handler.");
            }

            var administrationClient = await connectionService.GetOrCreateAdministrationClientAsync(cancellationToken);
            var allowDangerousFullCleanup = configuration.AllowDangerousFullCleanup;
            var topicNames = new List<string>();
            var queueNames = new List<string>();
            var subscriptions = new List<(string TopicName, string SubscriptionName)>();

            try
            {
                LogTargetScope();

                await foreach (var topic in administrationClient.GetTopicsAsync(cancellationToken))
                {
                    if (TransportFullCleanupNameFilter.CanDeleteAzureServiceBusQueueOrTopic(topic.Name, allowDangerousFullCleanup))
                    {
                        topicNames.Add(topic.Name);
                    }

                    await foreach (var subscription in administrationClient.GetSubscriptionsAsync(topic.Name, cancellationToken))
                    {
                        if (TransportFullCleanupNameFilter.CanDeleteAzureServiceBusSubscription(
                            subscription.SubscriptionName,
                            allowDangerousFullCleanup))
                        {
                            subscriptions.Add((subscription.TopicName, subscription.SubscriptionName));
                        }
                    }
                }

                await foreach (var queue in administrationClient.GetQueuesAsync(cancellationToken))
                {
                    if (TransportFullCleanupNameFilter.CanDeleteAzureServiceBusQueueOrTopic(queue.Name, allowDangerousFullCleanup))
                    {
                        queueNames.Add(queue.Name);
                    }
                }

                foreach (var subscription in subscriptions.Distinct())
                {
                    await DeleteSubscriptionAsync(administrationClient, subscription.TopicName, subscription.SubscriptionName, cancellationToken);
                }

                foreach (var topicName in topicNames.Distinct(StringComparer.Ordinal))
                {
                    await DeleteTopicAsync(administrationClient, topicName, cancellationToken);
                }

                foreach (var queueName in queueNames.Distinct(StringComparer.Ordinal))
                {
                    await DeleteQueueAsync(administrationClient, queueName, cancellationToken);
                }

                logger.LogInformation(
                    "Azure Service Bus full cleanup finished. Deleted {QueueCount} queues, {TopicCount} topics and {SubscriptionCount} subscriptions from namespace {Namespace}.",
                    queueNames.Count,
                    topicNames.Count,
                    subscriptions.Count,
                    GetNamespaceForLog());
            }
            finally
            {
                try
                {
                    await connectionService.CloseAdministrationClientAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to close Azure Service Bus administration client cleanly after full cleanup.");
                }
            }
        }

        private void LogTargetScope()
        {
            if (configuration.AllowDangerousFullCleanup)
            {
                logger.LogWarning(
                    "Azure Service Bus dangerous full cleanup is enabled. Target scope: all queues, topics and subscriptions in namespace {Namespace}.",
                    GetNamespaceForLog());
                return;
            }

            logger.LogInformation(
                "Azure Service Bus full cleanup target scope: queues/topics with prefix {EntityPrefix} and subscriptions with prefix {SubscriptionPrefix} in namespace {Namespace}.",
                TransportFullCleanupNameFilter.AzureServiceBusEntityPrefix,
                TransportFullCleanupNameFilter.AzureServiceBusSubscriptionPrefix,
                GetNamespaceForLog());
        }

        private string GetNamespaceForLog()
        {
            var connectionString = !string.IsNullOrWhiteSpace(configuration.AzureServiceBus.AdministrationConnectionString)
                ? configuration.AzureServiceBus.AdministrationConnectionString
                : configuration.AzureServiceBus.ConnectionString;
            var sections = AsbConnectionStringHelper.Parse(connectionString);

            return sections.TryGetValue("Endpoint", out var endpoint)
                ? endpoint
                : "configured namespace";
        }

        private async Task DeleteSubscriptionAsync(
            ServiceBusAdministrationClient administrationClient,
            string topicName,
            string subscriptionName,
            CancellationToken cancellationToken)
        {
            try
            {
                await administrationClient.DeleteSubscriptionAsync(topicName, subscriptionName, cancellationToken);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                logger.LogDebug(
                    ex,
                    "Azure Service Bus subscription {SubscriptionName} for topic {TopicName} was already removed during full cleanup.",
                    subscriptionName,
                    topicName);
            }
        }

        private async Task DeleteTopicAsync(
            ServiceBusAdministrationClient administrationClient,
            string topicName,
            CancellationToken cancellationToken)
        {
            try
            {
                await administrationClient.DeleteTopicAsync(topicName, cancellationToken);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                logger.LogDebug(ex, "Azure Service Bus topic {TopicName} was already removed during full cleanup.", topicName);
            }
        }

        private async Task DeleteQueueAsync(
            ServiceBusAdministrationClient administrationClient,
            string queueName,
            CancellationToken cancellationToken)
        {
            try
            {
                await administrationClient.DeleteQueueAsync(queueName, cancellationToken);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                logger.LogDebug(ex, "Azure Service Bus queue {QueueName} was already removed during full cleanup.", queueName);
            }
        }
    }
}
