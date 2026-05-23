using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Models.Enums;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Models.Enums;
using QsMessaging.Shared.Services.Interfaces;
using Polly;
using Polly.Retry;
using System.Collections.Concurrent;
using System.Text.Json;

namespace QsMessaging.AzureServiceBus
{
    internal class AsbSender(
        ILogger<AsbSender> logger,
        IAsbConnectionService connectionService,
        IAsbTopicService topicService,
        IAsbQueueService queueService,
        IQsMessagingConfiguration configuration,
        IHandlerService handlerService,
        Lazy<ISubscriber> subscriber,
        IRequestResponseMessageStore requestResponseMessageStore) : ISender
    {
        private readonly ConcurrentDictionary<string, ServiceBusSender> senders = new();

        public async Task SendMessageAsync<TMessage>(TMessage model) where TMessage : class
        {
            var topicName = await topicService.GetOrCreateTopicAsync(typeof(TMessage));
            var message = CreateMessage(model, typeof(TMessage), MessageTypeEnum.Message);
            if (await SendToEntityAsync(topicName, message, typeof(TMessage)))
            {
                logger.LogInformation("Message has been published to Azure Service Bus topic {TopicName}", topicName);
            }
        }

        public async Task SendEventAsync<TEvent>(TEvent model) where TEvent : class
        {
            var topicName = await topicService.GetOrCreateTopicAsync(typeof(TEvent));
            await SendToEntityRawAsync(topicName, CreateMessage(model, typeof(TEvent), MessageTypeEnum.Event));
            logger.LogInformation("Event has been published to Azure Service Bus topic {TopicName}", topicName);
        }

        public async Task<TResponse> SendRequest<TRequest, TResponse>(TRequest model, CancellationToken cancellationToken)
            where TRequest : class
            where TResponse : class
        {
            var correlationId = Guid.NewGuid().ToString("N");
            var waitForResponse = requestResponseMessageStore.AddRequestMessageAsync(correlationId, model, cancellationToken);

            var (responseHandlerRecord, isNew) = handlerService.AddRRResponseHandler<TResponse>();
            if (isNew)
            {
                await subscriber.Value.SubscribeHandlerAsync(responseHandlerRecord, cancellationToken);
            }

            //TODO: reconsidering  queue purpose type
            var requestQueueName = await queueService.GetOrCreateQueueAsync(typeof(TRequest), AsbQueuePurpose.Request, cancellationToken);
            var responseQueueName = await queueService.GetOrCreateQueueAsync(typeof(TResponse), AsbQueuePurpose.Response);

            var requestMessage = CreateMessage(model, typeof(TRequest), MessageTypeEnum.Message, correlationId, responseQueueName);
            await SendToEntityRawAsync(requestQueueName, requestMessage, cancellationToken);

            await waitForResponse;

            var response = requestResponseMessageStore.GetRespondedMessage<TResponse>(correlationId);
            requestResponseMessageStore.RemoveMessage(correlationId);
            return response;
        }

        public async Task SendMessageCorrelatedAsync(
            object model,
            string correlationId,
            string replyTo,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(replyTo))
            {
                logger.LogWarning("ReplyTo queue is empty, so the Azure Service Bus response cannot be sent.");
                return;
            }

            try
            {
                await SendToEntityRawAsync(replyTo, CreateMessage(model, model.GetType(), MessageTypeEnum.Message, correlationId) , cancellationToken);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                //TODO: Make exception. Maybe should be called error handler.
                logger.LogInformation(
                    "Azure Service Bus reply destination {ReplyToQueue} no longer exists. The response with correlation {CorrelationId} was ignored.",
                    replyTo,
                    correlationId);
            }
        }

        private async Task<bool> SendToEntityAsync(
            string queueOrTopicName,
            ServiceBusMessage message,
            Type contractType,
            CancellationToken cancellationToken = default)
        {
            var resilience = configuration.AzureServiceBus.Resilience;

            try
            {
                if (resilience.MaxRetryAttempts == 0)
                {
                    await SendToEntityRawAsync(queueOrTopicName, message, cancellationToken);
                }
                else
                {
                    var retryPipeline = new ResiliencePipelineBuilder()
                        .AddRetry(new RetryStrategyOptions
                        {
                            MaxRetryAttempts = resilience.MaxRetryAttempts,
                            Delay = resilience.Delay,
                            BackoffType = resilience.BackoffType,
                            UseJitter = resilience.UseJitter,
                            ShouldHandle = args =>
                            {
                                return ValueTask.FromResult(
                                    args.Outcome.Exception is ServiceBusException
                                    {
                                        Reason: ServiceBusFailureReason.MessagingEntityNotFound
                                    });
                            }
                        })
                        .Build();

                    await retryPipeline.ExecuteAsync(
                        async token => await SendToEntityRawAsync(queueOrTopicName, message, token),
                        cancellationToken);
                }

                return true;
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                logger.LogWarning(
                    ex,
                    "Azure Service Bus destination {EntityName} for message {MessageType} was not found after {RetryAttempts} retry attempts. The message was not published.",
                    queueOrTopicName,
                    contractType.FullName,
                    resilience.MaxRetryAttempts);

                return false;
            }
        }

        private async Task SendToEntityRawAsync(string queueOrTopicName, ServiceBusMessage message, CancellationToken cancellationToken = default)
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
