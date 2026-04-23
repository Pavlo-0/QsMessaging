using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Models.Enums;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Shared;
using QsMessaging.Shared.Models;

namespace QsMessaging.AzureServiceBus.Services
{
    internal class AsbServiceBusProcessorService(
        ILogger<AsbServiceBusProcessorService> logger,
        IAsbConnectionService connectionService,
        IAsbTopicService topicService,
        IAsbQueueService queueService,
        IAsbTopicSubscriptionService topicSubscriptionService) : IAsbServiceBusProcessorService
    {

        public async Task<ServiceBusProcessor> GetOrCreate(HandlersStoreRecord record, CancellationToken cancellationToken)
        {
            var reciverPurpose = HardConfiguration.GetReciverPurpose(record.supportedInterfacesType);
            var connection = await connectionService.GetOrCreateConnectionAsync(cancellationToken);

            ServiceBusProcessor? processor = null;

            switch (reciverPurpose)
            {
                case AsbReciverPurpose.QueueForRequest:
                    var queueName = await queueService.GetOrCreateQueueAsync(record.GenericType, AsbQueuePurpose.Request, cancellationToken);

                    processor = connection.CreateProcessor(queueName, CreateProcessorOptions());
                    break;
                case AsbReciverPurpose.QueueForResponse:
                    var responseQueueName = await queueService.GetOrCreateQueueAsync(record.GenericType, AsbQueuePurpose.Response, cancellationToken);

                    processor = connection.CreateProcessor(responseQueueName, CreateProcessorOptions());
                    break;
                case AsbReciverPurpose.TopicSubscription:

                    var topicName = await topicService.GetOrCreateTopicAsync(record.GenericType, cancellationToken);
                    var subscriptionName = await topicSubscriptionService.GetOrCreateSubscriptionAsync(record, cancellationToken);

                    processor = connection.CreateProcessor(topicName, subscriptionName, CreateProcessorOptions());

                    break;
                default:
                    throw new NotSupportedException("No Azure Service Bus receiver found for the specified handler.");
                    break;
            }

            return processor;
        }

        private static ServiceBusProcessorOptions CreateProcessorOptions()
        {
            return new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1
            };
        }
    }
}
