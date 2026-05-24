using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using System.Collections.Concurrent;

namespace QsMessaging.AzureServiceBus.Services
{
    internal class AsbTopicService(
        ILogger<AsbTopicService> logger,
        IAsbNameGeneratorService nameGenerator,
        IAsbConnectionService absConnectionService) : IAsbTopicService
    {
        private static readonly ConcurrentDictionary<string, byte> existingTopics = new();
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> topicLocks = new();

        public async Task<string> GetOrCreateTopicAsync(Type contractType, CancellationToken cancellationToken = default)
        {
            var topicName = nameGenerator.GetAsbTopicNameFromType(contractType);
            if (existingTopics.ContainsKey(topicName))
            {
                return topicName;
            }

            var topicLock = topicLocks.GetOrAdd(topicName, _ => new SemaphoreSlim(1, 1));
            await topicLock.WaitAsync(cancellationToken);
            try
            {
                if (existingTopics.ContainsKey(topicName))
                {
                    return topicName;
                }

                await CreateTopicIfMissingAsync(topicName, cancellationToken);
                existingTopics.TryAdd(topicName, 0);
                return topicName;
            }
            finally
            {
                topicLock.Release();
            }
        }

        public void InvalidateTopic(string topicName)
        {
            existingTopics.TryRemove(topicName, out _);
        }

        private async Task CreateTopicIfMissingAsync(string topicName, CancellationToken cancellationToken)
        {
            var client = await absConnectionService.GetOrCreateAdministrationClientAsync(cancellationToken);
            if (await client.TopicExistsAsync(topicName, cancellationToken))
            {
                return;
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
        }
    }
}
