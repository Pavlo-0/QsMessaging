using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using QsMessaging.Public;

namespace QsMessaging.AzureServiceBus
{
    internal sealed class AsbTransportFullCleaner(
        ILogger<AsbTransportFullCleaner> logger,
        Services.Interfaces.IAsbConnectionService connectionService) : IQsMessagingTransportFullCleaner
    {
        public async Task FullCleanUp(CancellationToken cancellationToken = default)
        {
            if (AsbMessageHandlerExecutionContext.IsInsideHandler)
            {
                throw new InvalidOperationException("Azure Service Bus full transport cleanup cannot run inside a message handler.");
            }

            var administrationClient = await connectionService.GetOrCreateAdministrationClientAsync(cancellationToken);
            var topicNames = new List<string>();
            var queueNames = new List<string>();
            var subscriptions = new List<(string TopicName, string SubscriptionName)>();

            try
            {
                await foreach (var topic in administrationClient.GetTopicsAsync(cancellationToken))
                {
                    topicNames.Add(topic.Name);

                    await foreach (var subscription in administrationClient.GetSubscriptionsAsync(topic.Name, cancellationToken))
                    {
                        subscriptions.Add((subscription.TopicName, subscription.SubscriptionName));
                    }
                }

                await foreach (var queue in administrationClient.GetQueuesAsync(cancellationToken))
                {
                    queueNames.Add(queue.Name);
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
                    "Azure Service Bus full cleanup finished. Deleted {QueueCount} queues, {TopicCount} topics and {SubscriptionCount} subscriptions.",
                    queueNames.Count,
                    topicNames.Count,
                    subscriptions.Count);
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
