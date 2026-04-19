using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Models.Enums;
using QsMessaging.AzureServiceBus.Services.Interfaces;

namespace QsMessaging.AzureServiceBus.Services
{
    internal sealed class AsbQueueService(
        ILogger<AsbQueueService> logger,
        IAsbNameGeneratorService nameGenerator, 
        IAsbConnectionService absConnectionService) : IAsbQueueService
    {
        public async Task<string> GetOrCreateQueueAsync(Type contractType, AsbQueuePurpose queuePurpose, CancellationToken cancellationToken = default)
        {
            var queueName = nameGenerator.GetAsbQueueNameFromType(contractType, queuePurpose);
            var queueOptions = new CreateQueueOptions(queueName)
            {
                AutoDeleteOnIdle = queuePurpose == AsbQueuePurpose.Response ? TimeSpan.FromMinutes(5) : TimeSpan.FromDays(14)
            };

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
