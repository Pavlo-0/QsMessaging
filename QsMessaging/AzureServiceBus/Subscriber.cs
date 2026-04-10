using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QsMessaging.Public.Handler;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.RabbitMq;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.RabbitMq.Services.Interfaces;
using System.Collections.Concurrent;
using System.Text.Json;

namespace QsMessaging.AzureServiceBus
{
    internal class Subscriber(
        ILogger<Subscriber> logger,
        IClientService clientService,
        IAdministrationService administrationService,
        IHandlerService handlerService,
        IServiceProvider services,
        IAzureServiceBusResponseSender responseSender) : IAzureServiceBusSubscriber, IAsyncDisposable
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
            var client = await clientService.GetOrCreateClientAsync(cancellationToken);
            var processorTarget = await GetProcessorTargetAsync(record, cancellationToken);
            var processorKey = processorTarget.Key;

            if (_processors.ContainsKey(processorKey))
            {
                return;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_processors.ContainsKey(processorKey))
                {
                    return;
                }

                var processor = processorTarget.IsSubscription
                    ? client.CreateProcessor(
                        processorTarget.EntityName,
                        processorTarget.SubscriptionName!,
                        CreateProcessorOptions())
                    : client.CreateProcessor(processorTarget.EntityName, CreateProcessorOptions());

                processor.ProcessMessageAsync += args => HandleMessageAsync(args, record, processorTarget.DisplayName);
                processor.ProcessErrorAsync += args => HandleProcessingErrorAsync(args, processorTarget.DisplayName);

                _processors[processorKey] = processor;
            }
            finally
            {
                _semaphore.Release();
            }

            await _processors[processorKey].StartProcessingAsync(cancellationToken);
        }

        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            foreach (var processor in _processors.Values)
            {
                try
                {
                    await processor.StopProcessingAsync(cancellationToken);
                    await processor.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to stop Azure Service Bus processor cleanly.");
                }
            }

            _processors.Clear();
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
                        await responseSender.SendResponseAsync(
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
            try
            {
                await args.CompleteMessageAsync(args.Message);
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
                var subscriptionName = await administrationService.GetOrCreateSubscriptionAsync(record, cancellationToken);
                return (
                    $"topic::{topicName}::{subscriptionName}",
                    topicName,
                    subscriptionName,
                    true,
                    $"{topicName}/{subscriptionName}");
            }

            var queuePurpose = HardConfiguration.GetQueuePurpose(record.supportedInterfacesType);
            var queueName = await administrationService.GetOrCreateQueueAsync(record.GenericType, queuePurpose, cancellationToken);
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
    }
}
