using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;
using QsMessaging.Public.Handler;
using QsMessaging.Shared;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services.Interfaces;

namespace QsMessaging.AzureServiceBus.Services
{
    internal class AsbConsumerService(
        ILogger<AsbSubscriber> logger,
        IServiceScopeFactory scopeFactory,
        IConsumerService consumerService,
        IQsMessagingConfiguration configuration) : IAsbConsumerService
    {

        public async Task HandleMessageAsync(
            ProcessMessageEventArgs args,
            HandlersStoreRecord record,
            AsbProcessorRegistration processorRegistration,
            CancellationToken cancellationToken)
        {

            try
            {
                using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    args.CancellationToken,
                    cancellationToken);

                await consumerService.UniversalConsumer(
                    data: args.Message.Body.ToMemory().ToArray(),
                    record: record,
                    context: CreateMessageContext(args.Message, processorRegistration),
                    cancellationToken: linkedCancellation.Token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while processing the message in HandleMessageAsync.");
            }
            finally
            {
                await CompleteMessageAsync(args);
            }
        }

        public async Task HandleProcessingErrorAsync(ProcessErrorEventArgs args, string entityDisplayName)
        {
            logger.LogError(
                args.Exception,
                "Azure Service Bus processor error on {EntityDisplayName}. Identifier: {Identifier}.",
                entityDisplayName,
                args.Identifier);

            if (!(configuration.FailedMessageHandling?.CallErrorHandlers ?? true))
            {
                return;
            }

            await ErrorAsync(
                             args.Exception,
                             new ErrorConsumerDetail(
                                 args.FullyQualifiedNamespace,
                                 null,
                                 entityDisplayName,
                                 args.EntityPath,
                                 args.Identifier,
                                 null,
                                 null,
                                 ErrorConsumerType.ReceivingProblem));

        }

        private async Task ErrorAsync(Exception ex, ErrorConsumerDetail model)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            foreach (var errorHandler in scope.ServiceProvider.GetServices<IQsMessagingConsumerErrorHandler>())
            {
                try
                {
                    await errorHandler.HandleErrorAsync(ex, model);
                }
                catch (Exception handlerException)
                {
                    logger.LogCritical(handlerException, "An exception occurred while handling Azure Service Bus consumer errors.");
                }
            }
        }

        private static async Task InvokeAsync(object? invokeResult)
        {
            if (invokeResult is Task task)
            {
                await task;
            }
        }

        private async Task CompleteMessageAsync(ProcessMessageEventArgs args)
        {
            if (args.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await args.CompleteMessageAsync(args.Message);
            }
            catch (ObjectDisposedException)
            {
                logger.LogDebug("Azure Service Bus connection was disposed before completing message {MessageId}.", args.Message.MessageId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to complete Azure Service Bus message {MessageId}.", args.Message.MessageId);
            }
        }

        private static ConsumerMessageContext CreateMessageContext(
            ServiceBusReceivedMessage message,
            AsbProcessorRegistration processorRegistration)
        {
            var applicationProperties = ConvertApplicationProperties(message.ApplicationProperties);
            var metadata = new Dictionary<string, string?>
            {
                ["DeliveryCount"] = message.DeliveryCount.ToString(),
                ["EnqueuedTimeUtc"] = message.EnqueuedTime.UtcDateTime.ToString("O"),
                ["SequenceNumber"] = message.SequenceNumber.ToString(),
                ["SubscriptionName"] = processorRegistration.SubscriptionName,
                ["EntityPath"] = processorRegistration.SubscriptionName is null
                    ? processorRegistration.EntityName
                    : $"{processorRegistration.DestinationName}/Subscriptions/{processorRegistration.SubscriptionName}"
            };

            return new ConsumerMessageContext
            {
                TransportName = "AzureServiceBus",
                OriginalQueueName = processorRegistration.EntityName,
                OriginalHashedQueueName = processorRegistration.EntityName,
                OriginalDestinationName = processorRegistration.DestinationName,
                OriginalHashedDestinationName = processorRegistration.DestinationName,
                Subject = message.Subject,
                ReplyTo = message.ReplyTo,
                CorrelationId = message.CorrelationId,
                MessageId = message.MessageId,
                ContentType = message.ContentType,
                ContentEncoding = TryGetHeader(applicationProperties, SerializationMetadata.ContentEncodingHeader),
                OriginalContractType = FirstNotEmpty(
                    message.Subject,
                    TryGetHeader(applicationProperties, SerializationMetadata.ContractTypeHeader)),
                Headers = applicationProperties,
                Metadata = metadata
            };
        }

        private static IReadOnlyDictionary<string, string?> ConvertApplicationProperties(
            IReadOnlyDictionary<string, object> applicationProperties)
        {
            return applicationProperties.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString());
        }

        private static string? TryGetHeader(IReadOnlyDictionary<string, string?> headers, string name)
        {
            return headers.TryGetValue(name, out var value) ? value : null;
        }

        private static string? FirstNotEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }
    }
}
