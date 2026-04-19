using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Models.Enums;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Shared;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services.Interfaces;

namespace QsMessaging.AzureServiceBus.Services
{
    internal sealed class AsbTopicSubscriptionService(
        ILogger<AsbTopicSubscriptionService> logger,
        IAsbNameGeneratorService nameGenerator,
        IAsbConnectionService absConnectionService,
        IHandlerService handlerService) : IAsbTopicSubscriptionService
    {
        public async Task<string> GetOrCreateSubscriptionAsync(HandlersStoreRecord record, CancellationToken cancellationToken = default)
        {
            var subscriptionPurpose = HardConfiguration.GetSubscriptionPurpose(record.supportedInterfacesType);

            var topicName = nameGenerator.GetAsbTopicNameFromType(record.GenericType);
            var subscriptionName = nameGenerator.GetSubscriptionName(record.GenericType, subscriptionPurpose);
            var client = await absConnectionService.GetOrCreateAdministrationClientAsync(cancellationToken);

            if (await client.SubscriptionExistsAsync(topicName, subscriptionName, cancellationToken))
            {
                return subscriptionName;
            }

            try
            {
                await client.CreateSubscriptionAsync(
                    new CreateSubscriptionOptions(topicName, subscriptionName)
                    {
                        AutoDeleteOnIdle = subscriptionPurpose == Models.Enums.AbsSubscriptionPurpose.Temporary ?  TimeSpan.FromMinutes(5) : TimeSpan.FromDays(14),
                    },
                    cancellationToken);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                //Skip it. Cause someone can create subscription after check, so ignore it
            }

            return subscriptionName;
        }

        public async Task DeleteTemporarySubscriptionsAsync(CancellationToken cancellationToken = default)
        {
            var subscriptionsToDelete = handlerService
                .GetHandlers()
                .Where(record => HardConfiguration.GetReciverPurpose(record.supportedInterfacesType) == AsbReciverPurpose.TopicSubscription && HardConfiguration.GetSubscriptionPurpose(record.supportedInterfacesType) == AbsSubscriptionPurpose.Temporary)
                .Select(record => (
                    TopicName: nameGenerator.GetAsbTopicNameFromType(record.GenericType),
                    SubscriptionName: nameGenerator.GetSubscriptionName(record.GenericType, AbsSubscriptionPurpose.Temporary)))
                .Distinct();

            var client = await absConnectionService.GetOrCreateAdministrationClientAsync(cancellationToken);

            foreach (var subscription in subscriptionsToDelete)
            {
                if (!await client.SubscriptionExistsAsync(subscription.TopicName, subscription.SubscriptionName, cancellationToken))
                {
                    continue;
                }

                try
                {
                    await client.DeleteSubscriptionAsync(subscription.TopicName, subscription.SubscriptionName, cancellationToken);
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
                {
                    logger.LogDebug(ex, "Azure Service Bus subscription {SubscriptionName} for topic {TopicName} was already removed.", subscription.SubscriptionName, subscription.TopicName);
                }
            }
        }
    }
}
