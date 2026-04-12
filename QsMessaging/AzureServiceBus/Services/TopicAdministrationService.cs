using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Shared.Interface;

namespace QsMessaging.AzureServiceBus.Services
{
    internal sealed class TopicAdministrationService : IAdministrationService
    {
        private readonly INameGenerator _nameGenerator;
        private readonly IAbsConnectionService _absConnectionService;
        private readonly SemaphoreSlim _semaphore = new(1,1);

        public TopicAdministrationService(INameGenerator nameGenerator, IAbsConnectionService absConnectionService)
        {
            _nameGenerator = nameGenerator ?? throw new ArgumentNullException(nameof(nameGenerator));
            _absConnectionService = absConnectionService ?? throw new ArgumentNullException(nameof(absConnectionService));
        }

        public async Task<string> GetOrCreateTopicAsync(Type contractType, CancellationToken cancellationToken = default)
        {
            /*
            var topicName = ServiceBusEntityNameFormatter.FormatTopicName(
                _nameGenerator.GetExchangeNameFromType(contractType));*/

            var topicName = _nameGenerator.GetAsbTopicNameFromType(contractType);

            var client = await _absConnectionService.GetOrCreateAdministrationClientAsync(cancellationToken).ConfigureAwait(false);
            if (await client.TopicExistsAsync(topicName, cancellationToken).ConfigureAwait(false))
            {
                return topicName;
            }

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!await client.TopicExistsAsync(topicName, cancellationToken).ConfigureAwait(false))
                {
                    try
                    {
                        await client.CreateTopicAsync(new CreateTopicOptions(topicName), cancellationToken).ConfigureAwait(false);
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

            return topicName;
        }
    }
}
