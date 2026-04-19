using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.RabbitMq.Models;

namespace QsMessaging.AzureServiceBus.Services
{
    internal sealed class AsbTopicSubscriptionService(
        IAsbNameGeneratorService nameGenerator,
        IAsbConnectionService absConnectionService) : IAsbTopicSubscriptionService
    {
        public async Task<string> GetOrCreateSubscriptionAsync(HandlersStoreRecord record, CancellationToken cancellationToken = default)
        {
            var topicName = nameGenerator.GetAsbTopicNameFromType(record.GenericType);
            var subscriptionName = nameGenerator.BuildSubscriptionName(record.GenericType);
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
                        AutoDeleteOnIdle = TimeSpan.FromMinutes(5)
                    },
                    cancellationToken);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                //Skip it. Cause someone can create subscription after check, so ignore it
            }

            return subscriptionName;
        }
    }
}
