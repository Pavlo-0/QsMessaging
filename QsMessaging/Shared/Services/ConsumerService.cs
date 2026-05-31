using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using QsMessaging.Public;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.Shared;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services.Interfaces;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;

namespace QsMessaging.Shared.Services
{
    internal class ConsumerService(
        ILogger<ConsumerService> logger,
        IServiceScopeFactory scopeFactory,
        IQsMessagingConfiguration configuration,
        IFailedMessageQueuePublisher failedMessageQueuePublisher,
        ISender responseSender) : IConsumerService
    {
        public async Task UniversalConsumer(
            byte[] data,
            HandlersStoreRecord record,
            ConsumerMessageContext context,
            CancellationToken cancellationToken)
        {
            byte[]? bodyBytes = null;
            object? modelInstance = null;
            var handlerErrors = new List<FailedMessageError>();

            try
            {
                await using var _ = MessageHandlerExecutionContext.Enter();

                var correlationId = context.CorrelationId ?? string.Empty;
                var replyTo = context.ReplyTo ?? string.Empty;

                bodyBytes = data;
                var message = Encoding.UTF8.GetString(bodyBytes);

                modelInstance = JsonSerializer.Deserialize(
                    message,
                    record.GenericType,
                    SerializationMetadata.GetJsonSerializerOptions(configuration));

                await using var scope = scopeFactory.CreateAsyncScope();
                var handlerInstance = scope.ServiceProvider.GetService(record.ConcreteHandlerInterfaceType)
                    ?? throw new InvalidOperationException($"Handler instance for {record.ConcreteHandlerInterfaceType} is null.");

                var consumerPurpose = HardConfiguration.GetConsumerPurpose(record.supportedInterfacesType);
                var consumeMethod = GetConsumerMethod(record, consumerPurpose)
                    ?? throw new NullReferenceException("Can't find Consumer method for handler.");
                object? responseModel = null;

                try
                {
                    responseModel = await InvokeHandlerWithRetryAsync(
                        consumerPurpose,
                        consumeMethod,
                        handlerInstance,
                        modelInstance,
                        correlationId,
                        record,
                        handlerErrors,
                        cancellationToken);
                }
                catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
                {
                    logger.LogDebug(ex, "Message handler processing was cancelled for {HandlerType}.", record.HandlerType.FullName);
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while processing the message handler in UniversalConsumer.");
                    await ErrorAsync(
                        ex,
                        new ErrorConsumerDetail(
                            modelInstance,
                            bodyBytes,
                            context.OriginalQueueName,
                            record.supportedInterfacesType.FullName,
                            record.ConcreteHandlerInterfaceType.FullName,
                            record.HandlerType.FullName,
                            record.GenericType.FullName,
                            ErrorConsumerType.InHandlerProblem),
                        BuildFailedMessageWrapper(
                            ex,
                            modelInstance,
                            bodyBytes,
                            record,
                            context,
                            ErrorConsumerType.InHandlerProblem,
                            handlerErrors),
                        cancellationToken);

                    return;
                }

                if (consumerPurpose == RqConsumerPurpose.RRRequestConsumer)
                {
                    await responseSender.SendMessageCorrelatedAsync(
                        responseModel ?? throw new NullReferenceException("RequestResponseHandler have to return result."),
                        correlationId,
                        replyTo,
                        cancellationToken);
                }
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "Message processing was cancelled.");
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error occurred while processing the message in UniversalConsumer.");
                await ErrorAsync(
                    e,
                    new ErrorConsumerDetail(
                        modelInstance,
                        bodyBytes,
                        context.OriginalQueueName,
                        record.supportedInterfacesType.FullName,
                        record.ConcreteHandlerInterfaceType.FullName,
                        record.HandlerType.FullName,
                        record.GenericType.FullName,
                        ErrorConsumerType.ReceivingProblem),
                    BuildFailedMessageWrapper(
                        e,
                        modelInstance,
                        bodyBytes,
                        record,
                        context,
                        ErrorConsumerType.ReceivingProblem,
                        handlerErrors,
                        handlerAttempts: 0),
                    cancellationToken);
            }
        }

        private async Task<object?> InvokeHandlerWithRetryAsync(
            RqConsumerPurpose consumerPurpose,
            MethodInfo consumeMethod,
            object handlerInstance,
            object? modelInstance,
            string correlationId,
            HandlersStoreRecord record,
            List<FailedMessageError> handlerErrors,
            CancellationToken cancellationToken)
        {
            var resilience = configuration.HandlerResilience;
            if (resilience.MaxRetryAttempts == 0)
            {
                return await InvokeHandlerAsync(consumerPurpose, consumeMethod, handlerInstance, modelInstance, correlationId, cancellationToken);
            }

            var retryPipeline = new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = resilience.MaxRetryAttempts,
                    Delay = resilience.Delay,
                    BackoffType = resilience.BackoffType,
                    UseJitter = resilience.UseJitter,
                    ShouldHandle = args => ValueTask.FromResult(
                        args.Outcome.Exception is not null
                        && !IsExpectedCancellation(args.Outcome.Exception, cancellationToken)),
                    OnRetry = args =>
                    {
                        if (args.Outcome.Exception is not null)
                        {
                            handlerErrors.Add(CreateFailedMessageError(args.Outcome.Exception));
                        }

                        logger.LogWarning(
                            args.Outcome.Exception,
                            "Message handler {HandlerType} failed. Retrying attempt {RetryAttempt}/{MaxRetryAttempts} after {RetryDelay}.",
                            record.HandlerType.FullName,
                            args.AttemptNumber + 1,
                            resilience.MaxRetryAttempts,
                            args.RetryDelay);

                        return default;
                    }
                })
                .Build();

            return await retryPipeline.ExecuteAsync(
                async _ => await InvokeHandlerAsync(consumerPurpose, consumeMethod, handlerInstance, modelInstance, correlationId, cancellationToken),
                cancellationToken);
        }

        private async ValueTask<object?> InvokeHandlerAsync(
            RqConsumerPurpose consumerPurpose,
            MethodInfo consumeMethod,
            object handlerInstance,
            object? modelInstance,
            string correlationId,
            CancellationToken cancellationToken)
        {
            var consumerArguments = CreateConsumerArguments(
                consumerPurpose,
                consumeMethod,
                modelInstance,
                correlationId,
                cancellationToken);

            switch (consumerPurpose)
            {
                case RqConsumerPurpose.MessageEventConsumer:
                    await InvokeAsync(InvokeConsumerMethod(consumeMethod, handlerInstance, consumerArguments));
                    return null;

                case RqConsumerPurpose.RRRequestConsumer:
                    var resultTask = InvokeConsumerMethod(consumeMethod, handlerInstance, consumerArguments);
                    if (resultTask is Task task)
                    {
                        await task;
                        return task.GetType().GetProperty("Result")?.GetValue(task)
                            ?? throw new NullReferenceException("RequestResponseHandler have to return result.");
                    }

                    logger.LogError("No Task<T> result was found. This is unexpected and may indicate an internal issue. Verify the RequestResponseHandler implementation.");
                    throw new NullReferenceException("No Task<T> result was found. This is unexpected and may indicate an internal issue. Verify the RequestResponseHandler implementation.");

                case RqConsumerPurpose.RRResponseConsumer:
                    await InvokeAsync(InvokeConsumerMethod(consumeMethod, handlerInstance, consumerArguments));
                    return null;

                default:
                    throw new NotSupportedException("No consumer found for the specified handler.");
            }
        }

        private static MethodInfo? GetConsumerMethod(HandlersStoreRecord record, RqConsumerPurpose consumerPurpose)
        {
            return GetConsumerMethod(record, consumerPurpose, acceptsCancellationToken: true)
                ?? GetConsumerMethod(record, consumerPurpose, acceptsCancellationToken: false);
        }

        private static MethodInfo? GetConsumerMethod(
            HandlersStoreRecord record,
            RqConsumerPurpose consumerPurpose,
            bool acceptsCancellationToken)
        {
            var parameterTypes = consumerPurpose switch
            {
                RqConsumerPurpose.MessageEventConsumer or RqConsumerPurpose.RRRequestConsumer => acceptsCancellationToken
                    ? new[] { record.GenericType, typeof(CancellationToken) }
                    : new[] { record.GenericType },
                RqConsumerPurpose.RRResponseConsumer => acceptsCancellationToken
                    ? new[] { typeof(object), typeof(string), typeof(CancellationToken) }
                    : new[] { typeof(object), typeof(string) },
                _ => Array.Empty<Type>()
            };

            return record.HandlerType.GetMethod(
                nameof(IQsMessageHandler<object>.Consumer),
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: parameterTypes,
                modifiers: null);
        }

        private static object?[] CreateConsumerArguments(
            RqConsumerPurpose consumerPurpose,
            MethodInfo consumeMethod,
            object? modelInstance,
            string correlationId,
            CancellationToken cancellationToken)
        {
            var acceptsCancellationToken = consumeMethod.GetParameters().LastOrDefault()?.ParameterType == typeof(CancellationToken);

            return consumerPurpose switch
            {
                RqConsumerPurpose.MessageEventConsumer or RqConsumerPurpose.RRRequestConsumer => acceptsCancellationToken
                    ? new object?[] { modelInstance, cancellationToken }
                    : new object?[] { modelInstance },
                RqConsumerPurpose.RRResponseConsumer => acceptsCancellationToken
                    ? new object?[] { modelInstance, correlationId, cancellationToken }
                    : new object?[] { modelInstance, correlationId },
                _ => Array.Empty<object?>()
            };
        }

        private static bool IsExpectedCancellation(Exception exception, CancellationToken cancellationToken)
        {
            return exception is OperationCanceledException && cancellationToken.IsCancellationRequested;
        }

        private static object? InvokeConsumerMethod(MethodInfo consumeMethod, object handlerInstance, object?[] consumerArguments)
        {
            try
            {
                return consumeMethod.Invoke(handlerInstance, consumerArguments);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        private static async Task InvokeAsync(object? invokeResult)
        {
            if (invokeResult is Task task)
            {
                await task;
            }
        }

        private async Task ErrorAsync(
            Exception ex,
            ErrorConsumerDetail model,
            FailedMessageWrapper failedMessage,
            CancellationToken cancellationToken)
        {
            var failedMessageHandling = configuration.FailedMessageHandling ?? new QsFailedMessageHandlingConfiguration();
            var routedToAnySink = false;

            if (failedMessageHandling.SendToErrorQueue)
            {
                routedToAnySink = true;
                try
                {
                    await failedMessageQueuePublisher.SendAsync(failedMessage, cancellationToken);
                }
                catch (Exception queueException)
                {
                    logger.LogError(
                        queueException,
                        "Failed to publish failed message {FailedEnvelopeId} to error queue {ErrorQueueName}.",
                        failedMessage.FailedEnvelopeId,
                        failedMessage.ErrorQueueName);
                }
            }

            if (failedMessageHandling.CallErrorHandlers)
            {
                routedToAnySink = true;
                await CallErrorHandlersAsync(ex, model with { FailedMessage = failedMessage });
            }

            if (!routedToAnySink)
            {
                logger.LogWarning(
                    "Failed message {FailedEnvelopeId} from {QueueName} was not routed because both failed-message sinks are disabled.",
                    failedMessage.FailedEnvelopeId,
                    model.QueueName);
            }
        }

        private async Task CallErrorHandlersAsync(Exception ex, ErrorConsumerDetail model)
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
                    logger.LogCritical(handlerException, "An exception occurred while handling Error. Check your error handlers.");
                }
            }
        }

        private FailedMessageWrapper BuildFailedMessageWrapper(
            Exception exception,
            object? modelInstance,
            byte[]? bodyBytes,
            HandlersStoreRecord record,
            ConsumerMessageContext context,
            ErrorConsumerType errorConsumerType,
            List<FailedMessageError> handlerErrors,
            int? handlerAttempts = null)
        {
            var errors = new List<FailedMessageError>(handlerErrors)
            {
                CreateFailedMessageError(exception)
            };

            var errorQueueName = failedMessageQueuePublisher.GetErrorQueueName(context);

            return new FailedMessageWrapper
            {
                TransportName = context.TransportName,
                OriginalQueueName = context.OriginalQueueName,
                OriginalHashedQueueName = context.OriginalHashedQueueName ?? context.OriginalQueueName,
                ErrorQueueName = errorQueueName,
                OriginalDestinationName = context.OriginalDestinationName,
                OriginalHashedDestinationName = context.OriginalHashedDestinationName ?? context.OriginalDestinationName,
                RoutingKey = context.RoutingKey,
                Subject = context.Subject,
                ReplyTo = context.ReplyTo,
                CorrelationId = context.CorrelationId,
                MessageId = context.MessageId,
                ContentType = context.ContentType,
                ContentEncoding = context.ContentEncoding,
                OriginalContractType = FirstNotEmpty(context.OriginalContractType, record.GenericType.FullName),
                HandlerType = record.HandlerType.FullName,
                ErrorConsumerType = errorConsumerType.ToString(),
                ErrorReason = exception.Message,
                OriginalMessagePayload = modelInstance,
                OriginalMessageBody = bodyBytes,
                OriginalMessageBodyText = GetBodyText(bodyBytes),
                OriginalMessageHeaders = context.Headers,
                HandlerAttempts = handlerAttempts ?? errors.Count,
                ConfiguredMaxRetryAttempts = configuration.HandlerResilience?.MaxRetryAttempts ?? 0,
                Errors = errors,
                CreatedUtc = DateTimeOffset.UtcNow,
                Metadata = context.Metadata
            };
        }

        private static FailedMessageError CreateFailedMessageError(Exception exception)
        {
            return new FailedMessageError
            {
                OccurredUtc = DateTimeOffset.UtcNow,
                ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                Message = exception.Message,
                StackTrace = exception.StackTrace,
                InnerException = exception.InnerException is null
                    ? null
                    : CreateFailedMessageError(exception.InnerException)
            };
        }

        private static string? GetBodyText(byte[]? bodyBytes)
        {
            return bodyBytes is null ? null : Encoding.UTF8.GetString(bodyBytes);
        }

        private static string? FirstNotEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }
    }
}
