using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Models.Enums;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using System.Collections.Concurrent;

namespace QsMessaging.AzureServiceBus.Services
{
    internal sealed class AsbQueueService(
        ILogger<AsbQueueService> logger,
        IAsbNameGeneratorService nameGenerator, 
        IAsbConnectionService absConnectionService) : IAsbQueueService
    {
        private static readonly ConcurrentDictionary<string, byte> existingQueues = new();
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> queueLocks = new();

        public async Task<string> GetOrCreateQueueAsync(Type contractType, AsbQueuePurpose queuePurpose, CancellationToken cancellationToken = default)
        {
            var queueName = nameGenerator.GetAsbQueueNameFromType(contractType, queuePurpose);
            if (existingQueues.ContainsKey(queueName))
            {
                return queueName;
            }

            var queueLock = queueLocks.GetOrAdd(queueName, _ => new SemaphoreSlim(1, 1));
            await queueLock.WaitAsync(cancellationToken);
            try
            {
                if (existingQueues.ContainsKey(queueName))
                {
                    return queueName;
                }

                await CreateQueueIfMissingAsync(queueName, queuePurpose, cancellationToken);
                existingQueues.TryAdd(queueName, 0);
                return queueName;
            }
            finally
            {
                queueLock.Release();
            }
        }

        public void InvalidateQueue(string queueName)
        {
            existingQueues.TryRemove(queueName, out _);
        }

        private async Task CreateQueueIfMissingAsync(string queueName, AsbQueuePurpose queuePurpose, CancellationToken cancellationToken)
        {
            var queueOptions = new CreateQueueOptions(queueName)
            {
                AutoDeleteOnIdle = queuePurpose == AsbQueuePurpose.Response ? TimeSpan.FromMinutes(5) : TimeSpan.FromDays(14)
            };

            var client = await absConnectionService.GetOrCreateAdministrationClientAsync(cancellationToken);
            if (await client.QueueExistsAsync(queueOptions.Name, cancellationToken))
            {
                return;
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
        }
    }
}
