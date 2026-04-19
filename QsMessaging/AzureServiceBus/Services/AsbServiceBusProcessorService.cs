using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Models.Enums;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.RabbitMq.Models;
using QsMessaging.Shared;
using System.Collections.Concurrent;

namespace QsMessaging.AzureServiceBus.Services
{
    internal class AsbServiceBusProcessorService(
        ILogger<AsbServiceBusProcessorService> logger,
        IAsbConnectionService connectionService,
        IAsbTopicService topicService,
        IAsbQueueService queueService,
        IAsbTopicSubscriptionService topicSubscriptionService) : IAsbServiceBusProcessorService
    {
        private readonly static ConcurrentBag<ServiceBusProcessor> _processors = new();

        public async Task<ServiceBusProcessor> GetOrCreate(HandlersStoreRecord record, CancellationToken cancellationToken)
        {
            var reciverPurpose = HardConfiguration.GetReciverPurpose(record.supportedInterfacesType);

            var client = await connectionService.GetOrCreateConnectionAsync(cancellationToken);

            ServiceBusProcessor? processor = null;

            switch (reciverPurpose)
            {
                case AsbReciverPurpose.QueueForRequest:
                    var queueName = await queueService.GetOrCreateQueueAsync(record.GenericType, AsbQueuePurpose.Request, cancellationToken);

                    processor = client.CreateProcessor(queueName, CreateProcessorOptions());
                    break;
                case AsbReciverPurpose.QueueForResponse:
                    var responseQueueName = await queueService.GetOrCreateQueueAsync(record.GenericType, AsbQueuePurpose.Response, cancellationToken);

                    processor = client.CreateProcessor(responseQueueName, CreateProcessorOptions());
                    break;
                case AsbReciverPurpose.TopicSubscription:

                    var topicName = await topicService.GetOrCreateTopicAsync(record.GenericType, cancellationToken);
                    var subscriptionName = await topicSubscriptionService.GetOrCreateSubscriptionAsync(record, cancellationToken);

                    processor = client.CreateProcessor(topicName, subscriptionName, CreateProcessorOptions());

                    break;
                default:
                    throw new NotSupportedException("No Azure Service Bus receiver found for the specified handler.");
                    break;
            }

            _processors.Add(processor);
            return processor;
        }

        public async Task StopAndDisposeProcessorAsync()
        {
            await Task.WhenAll(_processors.Select(StopAndDisposeProcessorAsync));
        }

        private static ServiceBusProcessorOptions CreateProcessorOptions()
        {
            return new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1
            };
        }

        private async Task StopAndDisposeProcessorAsync(ServiceBusProcessor processor)
        {
            try
            {
                if (!processor.IsClosed && processor.IsProcessing)
                {
                    // Use CancellationToken.None so the processor waits for in-flight handlers
                    // to complete before returning. Passing the external token can cause the stop
                    // to abort early, leaving handlers running while the connection is disposed.
                    await processor.StopProcessingAsync(CancellationToken.None);
                    await processor.DisposeAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to stop Azure Service Bus processor cleanly.");
            }
        }
    }
}
