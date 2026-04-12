//using Azure;
//using Azure.Messaging.ServiceBus;
//using Azure.Messaging.ServiceBus.Administration;
//using QsMessaging.AzureServiceBus.Services.Interfaces;
//using QsMessaging.Shared.Interface;
//using QsMessaging.Public;
//using System.Collections.Concurrent;
//using QsMessaging.RabbitMq;
//using QsMessaging.RabbitMq.Services.Interfaces;
//using QsMessaging.RabbitMq.Models;

//namespace QsMessaging.AzureServiceBus.Services
//{
//    //TODO: Service is transient there so no need semaphore. Remove it.
//    internal class AdministrationService(
//        IQsMessagingConfiguration configuration,
//        INameGenerator nameGenerator,
//        IInstanceService instanceService) : IAdministrationService, ISubscriptionService, IAsyncDisposable
//    {
//        private static readonly TimeSpan TemporaryEntityIdleTimeout = TimeSpan.FromMinutes(5);

//        private readonly SemaphoreSlim _semaphore = new(1, 1);
//        private readonly ConcurrentDictionary<string, (string TopicName, string SubscriptionName)> _ownedSubscriptions = new(StringComparer.OrdinalIgnoreCase);
//        private ServiceBusAdministrationClient? _administrationClient;

//        public async Task<string> GetOrCreateTopicAsync(Type contractType, CancellationToken cancellationToken = default)
//        {
//            var topicName = ServiceBusEntityNameFormatter.FormatTopicName(
//                nameGenerator.GetExchangeNameFromType(contractType));
//            var client = await GetOrCreateAdministrationClientAsync(cancellationToken);

//            if (await client.TopicExistsAsync(topicName, cancellationToken))
//            {
//                return topicName;
//            }

//            await _semaphore.WaitAsync(cancellationToken);
//            try
//            {
//                if (!await client.TopicExistsAsync(topicName, cancellationToken))
//                {
//                    try
//                    {
//                        await client.CreateTopicAsync(new CreateTopicOptions(topicName), cancellationToken);
//                    }
//                    catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
//                    {
//                    }
//                }
//            }
//            finally
//            {
//                _semaphore.Release();
//            }

//            return topicName;
//        }

//        public async Task<string> GetOrCreateSubscriptionAsync(HandlersStoreRecord record, CancellationToken cancellationToken = default)
//        {
//            var topicName = ServiceBusEntityNameFormatter.FormatTopicName(
//                nameGenerator.GetExchangeNameFromType(record.GenericType));
//            var subscriptionName = BuildSubscriptionName(record);
//            var client = await GetOrCreateAdministrationClientAsync(cancellationToken);

//            if (await client.SubscriptionExistsAsync(topicName, subscriptionName, cancellationToken))
//            {
//                return subscriptionName;
//            }

//            await _semaphore.WaitAsync(cancellationToken);
//            try
//            {
//                if (!await client.SubscriptionExistsAsync(topicName, subscriptionName, cancellationToken))
//                {
//                    try
//                    {
//                        await client.CreateSubscriptionAsync(
//                            new CreateSubscriptionOptions(topicName, subscriptionName)
//                            {
//                                AutoDeleteOnIdle = TemporaryEntityIdleTimeout
//                            },
//                            cancellationToken);
//                    }
//                    catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
//                    {
//                    }
//                }
//            }
//            finally
//            {
//                _semaphore.Release();
//            }

//            _ownedSubscriptions.TryAdd(subscriptionName, (topicName, subscriptionName));
//            return subscriptionName;
//        }
//        /*
//        public async Task DeleteOwnedEntitiesAsync(CancellationToken cancellationToken = default)
//        {
//            var client = await GetOrCreateAdministrationClientAsync(cancellationToken);

//            await queueAdministration.DeleteOwnedQueuesAsync(cancellationToken).ConfigureAwait(false);

//            foreach (var subscription in _ownedSubscriptions.Values)
//            {
//                try
//                {
//                    if (await client.SubscriptionExistsAsync(subscription.TopicName, subscription.SubscriptionName, cancellationToken))
//                    {
//                        await client.DeleteSubscriptionAsync(subscription.TopicName, subscription.SubscriptionName, cancellationToken);
//                    }
//                }
//                catch (RequestFailedException)
//                {
//                }
//            }
//            _ownedSubscriptions.Clear();
//        }*/

//        public async ValueTask DisposeAsync()
//        {
//            await DisposeAdministrationClientAsync();
//        }

//        // Queue-related helpers have been moved to QueueAdministration.

//        private string BuildSubscriptionName(HandlersStoreRecord record)
//        {
//            var rawName = $"{record.GenericType.FullName}:{record.HandlerType.FullName}:{instanceService.GetInstanceUID():N}";
//            var suffix = NameGenerator.HashString(rawName, 160);
//            return ServiceBusEntityNameFormatter.FormatSubscriptionName($"Qs:sub:{suffix}");
//        }

//        private async Task<ServiceBusAdministrationClient> GetOrCreateAdministrationClientAsync(CancellationToken cancellationToken)
//        {
//            if (_administrationClient is not null)
//            {
//                return _administrationClient;
//            }

//            await _semaphore.WaitAsync(cancellationToken);
//            try
//            {
//                _administrationClient ??= new ServiceBusAdministrationClient(
//                    ConnectionStringHelper.GetAdministrationConnectionString(configuration.AzureServiceBus));
//                return _administrationClient;
//            }
//            finally
//            {
//                _semaphore.Release();
//            }
//        }

//        private async Task DisposeAdministrationClientAsync()
//        {
//            switch (_administrationClient)
//            {
//                case IAsyncDisposable asyncDisposable:
//                    await asyncDisposable.DisposeAsync();
//                    break;
//                case IDisposable disposable:
//                    disposable.Dispose();
//                    break;
//            }

//            _administrationClient = null;
//        }
//    }
//}
