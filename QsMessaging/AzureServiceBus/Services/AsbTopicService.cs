using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;

namespace QsMessaging.AzureServiceBus.Services
{
    internal class AsbTopicService(
        ILogger<AsbTopicService> logger,
        IAsbNameGeneratorService nameGenerator,
        IAsbConnectionService absConnectionService) : IAsbTopicService
    {
        public async Task<string> GetOrCreateTopicAsync(Type contractType, CancellationToken cancellationToken = default)
        {
            var topicName = nameGenerator.GetAsbTopicNameFromType(contractType);
            var client = await absConnectionService.GetOrCreateAdministrationClientAsync(cancellationToken);
            if (await client.TopicExistsAsync(topicName, cancellationToken))
            {
                return topicName;
            }

            try
            {
                await client.CreateTopicAsync(new CreateTopicOptions(topicName), cancellationToken);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                //Skip it. Cause someone can create topic after check, so ignore it
            }
            logger.LogInformation("Topic '{TopicName}' created or already exists", topicName);
            return topicName;
        }
    }
}