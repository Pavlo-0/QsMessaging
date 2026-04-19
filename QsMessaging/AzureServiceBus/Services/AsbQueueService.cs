using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;

namespace QsMessaging.AzureServiceBus.Services
{
    internal sealed class AsbQueueService(
        ILogger<AsbQueueService> logger,
        IAsbNameGeneratorService nameGenerator, IAsbConnectionService absConnectionService) : IAsbQueueService
    {
        public async Task<string> GetOrCreateQueueAsync(Type contractType, CancellationToken cancellationToken = default)
        {
            var queueName = nameGenerator.GetAsbQueueNameFromType(contractType);
            var queueOptions = new CreateQueueOptions(queueName);

            var client = await absConnectionService.GetOrCreateAdministrationClientAsync();
            if (await client.QueueExistsAsync(queueOptions.Name, cancellationToken))
            {
                return queueName;
            }

            try
            {
                await client.CreateQueueAsync(queueOptions, cancellationToken);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                //Skip it. Cause someone can create queue after check, so ignore it
            }

            logger.LogInformation("Queue '{QueueName}' has been created or already exists.", queueName);
            return queueName;
        }
    }
}
