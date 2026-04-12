using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.Shared.Interface;

namespace QsMessaging.AzureServiceBus.Services
{
    internal sealed class QueueAdministration : IQueueAdministration
    {
        private static readonly TimeSpan TemporaryEntityIdleTimeout = TimeSpan.FromMinutes(5);
        private readonly ILogger<QueueAdministration> _logger;
        private readonly INameGenerator _nameGenerator;
        private readonly IAbsConnectionService _absConnectionService;

        public QueueAdministration(
            ILogger<QueueAdministration> logger,
            INameGenerator nameGenerator, IAbsConnectionService absConnectionService)
        {
            _logger = logger;
            _nameGenerator = nameGenerator ?? throw new ArgumentNullException(nameof(nameGenerator));
            _absConnectionService = absConnectionService ?? throw new ArgumentNullException(nameof(absConnectionService));
        }

        public async Task<string> GetOrCreateQueueAsync(Type contractType, QueuePurpose purpose, CancellationToken cancellationToken = default)
        {
            var queueName = GetQueueName(contractType, QueuePurpose.Permanent);
            var createOptions = CreateQueueOptions(queueName, purpose);

            await EnsureQueueExistsAsync(createOptions, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Queue '{QueueName}' has been created or already exists.", queueName);
            return queueName;
        }

        private string GetQueueName(Type contractType, QueuePurpose purpose)
        {
            return _nameGenerator.GetAsbQueueNameFromType(contractType);
        }

        private async Task EnsureQueueExistsAsync(CreateQueueOptions queueOptions, CancellationToken cancellationToken)
        {
            var client = await _absConnectionService.GetOrCreateAdministrationClientAsync();
            if (await client.QueueExistsAsync(queueOptions.Name, cancellationToken))
            {
                return;
            }

            try
            {
                await client.CreateQueueAsync(queueOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                // ignore concurrent create
            }
        }

        private CreateQueueOptions CreateQueueOptions(string queueName, QueuePurpose purpose)
        {
            var queueOptions = new CreateQueueOptions(queueName);
            /*
            if (purpose == QueuePurpose.SingleTemporary || purpose == QueuePurpose.InstanceTemporary)
            {
                queueOptions.AutoDeleteOnIdle = TemporaryEntityIdleTimeout;
            }*/

            return queueOptions;
        }
        
        /*
        private async Task<ServiceBusAdministrationClient> GetOrCreateAdministrationClientAsync(CancellationToken cancellationToken)
        {
            if (_administrationClient is not null)
            {
                return _administrationClient;
            }

                _administrationClient ??= new ServiceBusAdministrationClient(
                    ConnectionStringHelper.GetAdministrationConnectionString(_configuration.AzureServiceBus));
                return _administrationClient;
        }*/
        /*
        public async Task DeleteOwnedQueuesAsync(CancellationToken cancellationToken = default)
        {
            var client = await GetOrCreateAdministrationClientAsync(cancellationToken).ConfigureAwait(false);

            foreach (var queueName in _ownedQueues.Keys)
            {
                try
                {
                    if (await client.QueueExistsAsync(queueName, cancellationToken).ConfigureAwait(false))
                    {
                        await client.DeleteQueueAsync(queueName, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (RequestFailedException)
                {
                }
            }

            _ownedQueues.Clear();
        }*/

    }
}
