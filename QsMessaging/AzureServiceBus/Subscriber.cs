using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Services.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;
using AzureConnectionService = QsMessaging.AzureServiceBus.Services.Interfaces.IAbsConnectionService;

namespace QsMessaging.AzureServiceBus
{
    internal class AsbSubscriber(
        ILogger<AsbSubscriber> logger,
        AzureConnectionService connectionService,
        IAdministrationService administrationService,
        IQueueAdministration queueAdministration,
        ISubscriptionService subscriptionService,
        IHandlerService handlerService,
        IServiceProvider services,
        ISender responseSender) : ISubscriber, IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly ConcurrentDictionary<string, ServiceBusProcessor> _processors = new(StringComparer.OrdinalIgnoreCase);

        public async Task SubscribeAsync(CancellationToken cancellationToken = default)
        {
            foreach (var record in handlerService.GetHandlers())
            {
                await SubscribeHandlerAsync(record, cancellationToken);
            }
        }

        public async Task SubscribeHandlerAsync(HandlersStoreRecord record, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Subscribing handler to the message queue.");
            logger.LogDebug("{Type}", record.GenericType.FullName);

            var processorTarget = await GetProcessorTargetAsync(record, cancellationToken);
            var processorKey = processorTarget.Key;

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_processors.ContainsKey(processorKey))
                {
                    return;
                }

                var client = await GetClientAsync(cancellationToken);
                var processor = processorTarget.IsSubscription
                    ? client.CreateProcessor(
                        processorTarget.EntityName,
                        processorTarget.SubscriptionName!,
                        CreateProcessorOptions())
                    : client.CreateProcessor(processorTarget.EntityName, CreateProcessorOptions());

                processor.ProcessMessageAsync += args => HandleMessageAsync(args, record, processorTarget.DisplayName);
                processor.ProcessErrorAsync += args => HandleProcessingErrorAsync(args, processorTarget.DisplayName);

                await processor.StartProcessingAsync(cancellationToken);
                _processors[processorKey] = processor;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                foreach (var processor in _processors.Values)
                {
                    await StopAndDisposeProcessorAsync(processor, cancellationToken);
                }

                _processors.Clear();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await CloseAsync();
            _semaphore.Dispose();
        }

        private async Task HandleMessageAsync(ProcessMessageEventArgs args, HandlersStoreRecord record, string entityDisplayName)
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

        private Task HandleProcessingErrorAsync(ProcessErrorEventArgs args, string entityDisplayName)
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

        private async Task<(string Key, string EntityName, string? SubscriptionName, bool IsSubscription, string DisplayName)>
            GetProcessorTargetAsync(HandlersStoreRecord record, CancellationToken cancellationToken)
        {
            if (record.supportedInterfacesType == typeof(IQsEventHandler<>))
            {
                var topicName = await administrationService.GetOrCreateTopicAsync(record.GenericType, cancellationToken);
                var subscriptionName = await subscriptionService.GetOrCreateSubscriptionAsync(record, cancellationToken);
                return (
                    $"topic::{topicName}::{subscriptionName}",
                    topicName,
                    subscriptionName,
                    true,
                    $"{topicName}/{subscriptionName}");
            }

            //var queuePurpose = HardConfiguration.GetQueuePurpose(record.supportedInterfacesType);
            var queueName = await queueAdministration.GetOrCreateQueueAsync(record.GenericType, QueuePurpose.Permanent, cancellationToken);
            return ($"queue::{queueName}", queueName, null, false, queueName);
        }

        private static ServiceBusProcessorOptions CreateProcessorOptions()
        {
            return new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1
            };
        }

        private async Task StopAndDisposeProcessorAsync(ServiceBusProcessor processor, CancellationToken cancellationToken)
        {
            try
            {
                if (!processor.IsClosed && processor.IsProcessing)
                {
                    // Use CancellationToken.None so the processor waits for in-flight handlers
                    // to complete before returning. Passing the external token can cause the stop
                    // to abort early, leaving handlers running while the connection is disposed.
                    await processor.StopProcessingAsync(CancellationToken.None);
                }
            }
            catch (ObjectDisposedException)
            {
                logger.LogDebug("Azure Service Bus processor was already disposed during shutdown.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to stop Azure Service Bus processor cleanly.");
            }

            try
            {
                if (!processor.IsClosed)
                {
                    await processor.DisposeAsync();
                }
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, nothing to do.
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to dispose Azure Service Bus processor.");
            }
        }

        private async Task<ServiceBusClient> GetClientAsync(CancellationToken cancellationToken)
        {
            return await connectionService.GetOrCreateConnectionAsync(cancellationToken);
        }
    }
}
