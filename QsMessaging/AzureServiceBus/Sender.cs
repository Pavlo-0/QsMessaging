using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Transporting.Interfaces;
using System.Text.Json;

namespace QsMessaging.AzureServiceBus
{
    internal class Sender(
        ILogger<Sender> logger,
        IClientService clientService,
        IAdministrationService administrationService,
        IHandlerService handlerService,
        Lazy<IAzureServiceBusSubscriber> subscriber,
        IRequestResponseMessageStore requestResponseMessageStore) : ITransportSender, IAzureServiceBusResponseSender
    {
        public async Task SendMessageAsync<TMessage>(TMessage model) where TMessage : class
        {
            var queueName = await administrationService.GetOrCreateQueueAsync(typeof(TMessage), QueuePurpose.Permanent);
            await SendToEntityAsync(queueName, CreateMessage(model, typeof(TMessage)));
            logger.LogInformation("Message has been published to Azure Service Bus queue {QueueName}", queueName);
        }

        public async Task SendEventAsync<TEvent>(TEvent model) where TEvent : class
        {
            var topicName = await administrationService.GetOrCreateTopicAsync(typeof(TEvent));
            await SendToEntityAsync(topicName, CreateMessage(model, typeof(TEvent)));
            logger.LogInformation("Event has been published to Azure Service Bus topic {TopicName}", topicName);
        }

        public async Task<TResponse> SendRequest<TRequest, TResponse>(TRequest model, CancellationToken cancellationToken)
            where TRequest : class
            where TResponse : class
        {
            var correlationId = Guid.NewGuid().ToString("N");
            var waitForResponse = requestResponseMessageStore.AddRequestMessageAsync(correlationId, model, cancellationToken);

            var responseHandlerRecord = handlerService.AddRRResponseHandler<TResponse>();
            await subscriber.Value.SubscribeHandlerAsync(responseHandlerRecord, cancellationToken);

            var requestQueueName = administrationService.GetQueueName(typeof(TRequest), QueuePurpose.SingleTemporary);
            if (await administrationService.QueueExistsAsync(requestQueueName, cancellationToken))
            {
                var responseQueueName = administrationService.GetQueueName(typeof(TResponse), QueuePurpose.InstanceTemporary);
                var requestMessage = CreateMessage(model, typeof(TRequest), correlationId, responseQueueName);
                await SendToEntityAsync(requestQueueName, requestMessage, cancellationToken);
            }
            else
            {
                logger.LogWarning(
                    "Azure Service Bus request queue {QueueName} does not exist. The request will time out if no consumer creates it.",
                    requestQueueName);
            }

            await waitForResponse;

            var response = requestResponseMessageStore.GetRespondedMessage<TResponse>(correlationId);
            requestResponseMessageStore.RemoveMessage(correlationId);
            return response;
        }

        public async Task SendResponseAsync(object model, string correlationId, string replyTo, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(replyTo))
            {
                logger.LogWarning("ReplyTo queue is empty, so the Azure Service Bus response cannot be sent.");
                return;
            }

            try
            {
                await SendToEntityAsync(replyTo, CreateMessage(model, model.GetType(), correlationId), cancellationToken);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                logger.LogInformation(
                    "Azure Service Bus reply destination {ReplyTo} no longer exists. The response with correlation {CorrelationId} was ignored.",
                    replyTo,
                    correlationId);
            }
        }

        private async Task SendToEntityAsync(string entityName, ServiceBusMessage message, CancellationToken cancellationToken = default)
        {
            var client = await clientService.GetOrCreateClientAsync(cancellationToken);
            await using var sender = client.CreateSender(entityName);
            await sender.SendMessageAsync(message, cancellationToken);
        }

        private static ServiceBusMessage CreateMessage(object model, Type contractType, string? correlationId = null, string? replyTo = null)
        {
            var message = new ServiceBusMessage(BinaryData.FromString(JsonSerializer.Serialize(model)))
            {
                ContentType = "application/json",
                Subject = contractType.FullName,
                CorrelationId = correlationId,
                ReplyTo = replyTo
            };

            return message;
        }
    }
}
