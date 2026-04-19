using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Models.Enums;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Models.Enums;
using QsMessaging.Shared.Services.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;

namespace QsMessaging.AzureServiceBus
{
    internal class AsbSender(
        ILogger<AsbSender> logger,
        IAsbConnectionService connectionService,
        IAsbTopicService topicService,
        IAsbQueueService queueService,
        IHandlerService handlerService,
        Lazy<ISubscriber> subscriber,
        IRequestResponseMessageStore requestResponseMessageStore) : ISender
    {
        private readonly ConcurrentDictionary<string, ServiceBusSender> senders = new();

        public async Task SendMessageAsync<TMessage>(TMessage model) where TMessage : class
        {
            var queueName = await topicService.GetOrCreateTopicAsync(typeof(TMessage));
            await SendToEntityAsync(queueName, CreateMessage(model, typeof(TMessage), MessageTypeEnum.Event));
            logger.LogInformation("Message has been published to Azure Service Bus queue {QueueName}", queueName);
        }

        public async Task SendEventAsync<TEvent>(TEvent model) where TEvent : class
        {
            var topicName = await topicService.GetOrCreateTopicAsync(typeof(TEvent));
            await SendToEntityAsync(topicName, CreateMessage(model, typeof(TEvent), MessageTypeEnum.Event));
            logger.LogInformation("Event has been published to Azure Service Bus topic {TopicName}", topicName);
        }

        public async Task<TResponse> SendRequest<TRequest, TResponse>(TRequest model, CancellationToken cancellationToken)
            where TRequest : class
            where TResponse : class
        {
            var correlationId = Guid.NewGuid().ToString("N");
            var waitForResponse = requestResponseMessageStore.AddRequestMessageAsync(correlationId, model, cancellationToken);

            //TODO: Should we remove at the end handler from here or check if exists or not to avoid duplicate. What if parallel requests
            var responseHandlerRecord = handlerService.AddRRResponseHandler<TResponse>();
            await subscriber.Value.SubscribeHandlerAsync(responseHandlerRecord, cancellationToken);

            //TODO: reconsidering  queue purpose type
            var requestQueueName = await queueService.GetOrCreateQueueAsync(typeof(TRequest), AsbQueuePurpose.Request, cancellationToken);
            var responseQueueName = await queueService.GetOrCreateQueueAsync(typeof(TResponse), AsbQueuePurpose.Response);

            var requestMessage = CreateMessage(model, typeof(TRequest), MessageTypeEnum.Message, correlationId, responseQueueName);
            await SendToEntityAsync(requestQueueName, requestMessage, cancellationToken);

            await waitForResponse;

            var response = requestResponseMessageStore.GetRespondedMessage<TResponse>(correlationId);
            requestResponseMessageStore.RemoveMessage(correlationId);
            return response;
        }

        public async Task SendMessageCorrelationAsync(
            object model,
            string correlationId,
            string? replyTo = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(replyTo))
            {
                logger.LogWarning("ReplyTo queue is empty, so the Azure Service Bus response cannot be sent.");
                return;
            }

            try
            {
                await SendToEntityAsync(replyTo, CreateMessage(model, model.GetType(), MessageTypeEnum.Message, correlationId) , cancellationToken);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                //TODO: Make exception. Maybe should be called error handler.
                logger.LogInformation(
                    "Azure Service Bus reply destination {ReplyTo} no longer exists. The response with correlation {CorrelationId} was ignored.",
                    replyTo,
                    correlationId);
            }
        }

        private async Task SendToEntityAsync(string queueOrTopicName, ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            var client = await connectionService.GetOrCreateConnectionAsync(cancellationToken);
            var sender = senders.GetOrAdd(queueOrTopicName, client.CreateSender);
            await sender.SendMessageAsync(message, cancellationToken);
        }

        private static ServiceBusMessage CreateMessage(object model, Type contractType, MessageTypeEnum messageType, string? correlationId = null, string? replyTo = null)
        {
            var message = new ServiceBusMessage(BinaryData.FromString(JsonSerializer.Serialize(model)))
            {
                ContentType = "application/json",
                Subject = contractType.FullName, //TODO: Can be type of message for queue topic event or message and etc
                TimeToLive = messageType == MessageTypeEnum.Message ? TimeSpan.FromDays(14) : TimeSpan.FromSeconds(60),
                CorrelationId = correlationId,
                ReplyTo = replyTo
            };

            return message;
        }
    }
}
