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

        public async Task<AsbProcessorRegistration> GetOrCreate(HandlersStoreRecord record, CancellationToken cancellationToken)
        {
            var reciverPurpose = HardConfiguration.GetReciverPurpose(record.supportedInterfacesType);
            logger.LogDebug(
                "Creating Azure Service Bus processor for {HandlerType} with receiver purpose {ReceiverPurpose}.",
                record.HandlerType.FullName,
                reciverPurpose);
            var connection = await connectionService.GetOrCreateConnectionAsync(cancellationToken);

            switch (reciverPurpose)
            {
                case AsbReciverPurpose.QueueForRequest:
                    var queueName = await queueService.GetOrCreateQueueAsync(record.GenericType, AsbQueuePurpose.Request, cancellationToken);

                    return new AsbProcessorRegistration(
                        connection.CreateProcessor(queueName, CreateProcessorOptions()),
                        queueName,
                        queueName,
                        null);
                case AsbReciverPurpose.QueueForResponse:
                    var responseQueueName = await queueService.GetOrCreateQueueAsync(record.GenericType, AsbQueuePurpose.Response, cancellationToken);

                    return new AsbProcessorRegistration(
                        connection.CreateProcessor(responseQueueName, CreateProcessorOptions()),
                        responseQueueName,
                        responseQueueName,
                        null);
                case AsbReciverPurpose.TopicSubscription:

                    var topicName = await topicService.GetOrCreateTopicAsync(record.GenericType, cancellationToken);
                    var subscriptionName = await topicSubscriptionService.GetOrCreateSubscriptionAsync(record, cancellationToken);

                    return new AsbProcessorRegistration(
                        connection.CreateProcessor(topicName, subscriptionName, CreateProcessorOptions()),
                        subscriptionName,
                        topicName,
                        subscriptionName);
                default:
                    throw new NotSupportedException("No Azure Service Bus receiver found for the specified handler.");
            }
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
