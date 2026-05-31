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
            var autoDeleteOnIdle = queuePurpose == AsbQueuePurpose.Response ? TimeSpan.FromMinutes(5) : TimeSpan.FromDays(14);
            return await GetOrCreateQueueAsync(queueName, autoDeleteOnIdle, cancellationToken);
        }

        public async Task<string> GetOrCreateQueueAsync(string queueName, CancellationToken cancellationToken = default)
        {
            return await GetOrCreateQueueAsync(queueName, TimeSpan.FromDays(14), cancellationToken);
        }

        private async Task<string> GetOrCreateQueueAsync(
            string queueName,
            TimeSpan autoDeleteOnIdle,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(queueName))
            {
                throw new ArgumentException("Queue name can not be empty.", nameof(queueName));
            }

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

                await CreateQueueIfMissingAsync(queueName, autoDeleteOnIdle, cancellationToken);
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

        private async Task CreateQueueIfMissingAsync(string queueName, TimeSpan autoDeleteOnIdle, CancellationToken cancellationToken)
        {
            var queueOptions = new CreateQueueOptions(queueName)
            {
                AutoDeleteOnIdle = autoDeleteOnIdle
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
