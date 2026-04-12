using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Shared.Interface;

namespace QsMessaging.AzureServiceBus.Services
{
    internal sealed class SubscriptionAdministrationService : ISubscriptionService
    {
        private readonly INameGenerator _nameGenerator;
        private readonly IAbsConnectionService _absConnectionService;
        private readonly IInstanceService _instanceService;
        private readonly SemaphoreSlim _semaphore = new(1,1);

        public SubscriptionAdministrationService(INameGenerator nameGenerator, IAbsConnectionService absConnectionService, IInstanceService instanceService)
        {
            _nameGenerator = nameGenerator ?? throw new ArgumentNullException(nameof(nameGenerator));
            _absConnectionService = absConnectionService ?? throw new ArgumentNullException(nameof(absConnectionService));
            _instanceService = instanceService ?? throw new ArgumentNullException(nameof(instanceService));
        }

        public async Task<string> GetOrCreateSubscriptionAsync(HandlersStoreRecord record, CancellationToken cancellationToken = default)
        {
            /*var topicName = ServiceBusEntityNameFormatter.FormatTopicName(
                _nameGenerator.GetExchangeNameFromType(record.GenericType));*/
            var topicName = _nameGenerator.GetAsbTopicNameFromType(record.GenericType);
            var subscriptionName = BuildSubscriptionName(record);
            var client = await _absConnectionService.GetOrCreateAdministrationClientAsync(cancellationToken).ConfigureAwait(false);

            if (await client.SubscriptionExistsAsync(topicName, subscriptionName, cancellationToken).ConfigureAwait(false))
            {
                return subscriptionName;
            }

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!await client.SubscriptionExistsAsync(topicName, subscriptionName, cancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        await client.CreateSubscriptionAsync(
                            new CreateSubscriptionOptions(topicName, subscriptionName)
                            {
                                AutoDeleteOnIdle = TimeSpan.FromMinutes(5)
                            },
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                    {
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }

            return subscriptionName;
        }

        private string BuildSubscriptionName(HandlersStoreRecord record)
        {
            var rawName = $"{record.GenericType.FullName}:{record.HandlerType.FullName}:{_instanceService.GetInstanceUID():N}";
            var suffix = NameGenerator.HashString(rawName, 160);
            return ServiceBusEntityNameFormatter.FormatSubscriptionName($"Qs:sub:{suffix}");
        }
    }
}
