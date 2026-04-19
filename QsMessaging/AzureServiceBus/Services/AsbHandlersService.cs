using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.Shared;
using System.Text.Json;

namespace QsMessaging.AzureServiceBus.Services
{
    internal class AsbHandlersService(
        ILogger<AsbSubscriber> logger,
        IServiceProvider services,
        ISender responseSender) : IAsbHandlersService
    {

        public async Task HandleMessageAsync(ProcessMessageEventArgs args, HandlersStoreRecord record, string entityDisplayName)
        {
            object? modelInstance = null;
            var bodyBytes = args.Message.Body.ToMemory().ToArray();
            using var _ = AsbMessageHandlerExecutionContext.Enter();

            try
            {
                modelInstance = JsonSerializer.Deserialize(args.Message.Body.ToString(), record.GenericType);

                var consumeMethod = record.HandlerType.GetMethod(nameof(IQsMessageHandler<object>.Consumer))
                    ?? throw new NullReferenceException("Can't find Consumer method for handler.");
                var handlerInstance = services.GetService(record.ConcreteHandlerInterfaceType)
                    ?? throw new InvalidOperationException($"Handler instance for {record.ConcreteHandlerInterfaceType} is null.");

                switch (HardConfiguration.GetConsumerPurpose(record.supportedInterfacesType))
                {
                    case ConsumerPurpose.MessageEventConsumer:
                        await InvokeAsync(consumeMethod.Invoke(handlerInstance, new[] { modelInstance }));
                        break;

                    case ConsumerPurpose.RRRequestConsumer:
                        var resultTask = consumeMethod.Invoke(handlerInstance, new[] { modelInstance });
                        if (resultTask is not Task task)
                        {
                            throw new NullReferenceException("RequestResponseHandler have to return Task<T>.");
                        }

                        await task;
                        var responseModel = task.GetType().GetProperty("Result")?.GetValue(task)
                            ?? throw new NullReferenceException("RequestResponseHandler have to return result.");
                        await responseSender.SendMessageCorrelationAsync(
                            responseModel,
                            args.Message.CorrelationId ?? string.Empty,
                            args.Message.ReplyTo ?? string.Empty,
                            args.CancellationToken);
                        break;

                    case ConsumerPurpose.RRResponseConsumer:
                        await InvokeAsync(consumeMethod.Invoke(handlerInstance, new object?[] { modelInstance, args.Message.CorrelationId ?? string.Empty }));
                        break;

                    default:
                        throw new NotSupportedException("No Azure Service Bus consumer found for the specified handler.");
                }
            }
            catch (Exception ex)
            {
                await ErrorAsync(
                    ex,
                    new ErrorConsumerDetail(
                        modelInstance,
                        bodyBytes,
                        entityDisplayName,
                        record.supportedInterfacesType.FullName,
                        record.ConcreteHandlerInterfaceType.FullName,
                        record.HandlerType.FullName,
                        record.GenericType.FullName,
                        ErrorConsumerType.RecevingProblem));
            }
            finally
            {
                await CompleteMessageAsync(args);
            }
        }

        public Task HandleProcessingErrorAsync(ProcessErrorEventArgs args, string entityDisplayName)
        {
            if (args.Exception is ObjectDisposedException)
            {
                logger.LogDebug(
                    "Azure Service Bus processor is stopping for {EntityDisplayName}. Identifier: {Identifier}.",
                    entityDisplayName,
                    args.Identifier);
                return Task.CompletedTask;
            }

            logger.LogError(
                args.Exception,
                "Azure Service Bus processor error on {EntityDisplayName}. Identifier: {Identifier}.",
                entityDisplayName,
                args.Identifier);
            return Task.CompletedTask;
        }

        private async Task ErrorAsync(Exception ex, ErrorConsumerDetail model)
        {
            try
            {
                foreach (var errorHandler in services.GetServices<IQsMessagingConsumerErrorHandler>())
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
