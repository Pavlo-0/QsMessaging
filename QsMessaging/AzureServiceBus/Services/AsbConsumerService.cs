using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.Shared;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services.Interfaces;
using System.Text.Json;

namespace QsMessaging.AzureServiceBus.Services
{
    internal class AsbConsumerService(
        ILogger<AsbSubscriber> logger,
        IServiceScopeFactory scopeFactory,
        IConsumerService consumerService,
        ISender responseSender) : IAsbConsumerService
    {

        public async Task HandleMessageAsync(ProcessMessageEventArgs args, HandlersStoreRecord record, string entityDisplayName, CancellationToken cancellationToken)
        {

            try
            {
                await consumerService.UniversalConsumer(
                    data: args.Message.Body.ToMemory().ToArray(),
                    record: record,
                    correlationId: args.Message.CorrelationId,
                    replyTo: args.Message.ReplyTo,
                    name: entityDisplayName,
                    cancellationToken: cancellationToken);
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
                                 ErrorConsumerType.RecevingProblem));

        }

        private async Task ErrorAsync(Exception ex, ErrorConsumerDetail model)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                foreach (var errorHandler in scope.ServiceProvider.GetServices<IQsMessagingConsumerErrorHandler>())
                {
                    await errorHandler.HandleErrorAsync(ex, model);
                }
            }
            catch (Exception handlerException)
            {
                logger.LogCritical(handlerException, "An exception occurred while handling Azure Service Bus consumer errors.");
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
    }
}
