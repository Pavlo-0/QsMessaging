using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services.Interfaces;
using System.Text;
using System.Text.Json;

namespace QsMessaging.Shared.Services
{
    internal class ConsumerService(
        ILogger<ConsumerService> logger,
        IServiceScopeFactory scopeFactory,
        ISender responseSender) : IConsumerService
    {
        public async Task UniversalConsumer(byte[] data, HandlersStoreRecord record, string? correlationId, string replyTo, string name, CancellationToken cancellationToken)
        {

            try
            {
                await using var _ = MessageHandlerExecutionContext.Enter();

                correlationId = correlationId ?? string.Empty;

                byte[] bodyBytes = data;
                var message = Encoding.UTF8.GetString(bodyBytes);

                object? modelInstance = null;
                modelInstance = JsonSerializer.Deserialize(message, record.GenericType);

                var consumeMethod = record.HandlerType.GetMethod(nameof(IQsMessageHandler<object>.Consumer))
                    ?? throw new NullReferenceException("Can't find Consumer method for handler.");
                await using var scope = scopeFactory.CreateAsyncScope();
                var handlerInstance = scope.ServiceProvider.GetService(record.ConcreteHandlerInterfaceType)
                    ?? throw new InvalidOperationException($"Handler instance for {record.ConcreteHandlerInterfaceType} is null.");

                try
                {
                    switch (HardConfiguration.GetConsumerPurpose(record.supportedInterfacesType))
                    {
                        case RqConsumerPurpose.MessageEventConsumer:
                            await InvokeAsync(consumeMethod.Invoke(handlerInstance, new[] { modelInstance }));
                            break;

                        case RqConsumerPurpose.RRRequestConsumer:
                            var resultTask = consumeMethod.Invoke(handlerInstance, new[] { modelInstance });
                            if (resultTask is Task task)
                            {
                                await task;
                                var responseModel = task.GetType().GetProperty("Result")?.GetValue(task)
                                    ?? throw new NullReferenceException("RequestResponseHandler have to return result.");
                                await responseSender.SendMessageCorrelatedAsync(
                                    responseModel,
                                    correlationId,
                                    replyTo,
                                    cancellationToken);

                            }
                            else
                            {
                                logger.LogError("No Task<T> result was found. This is unexpected and may indicate an internal issue. Verify the RequestResponseHandler implementation.");
                                throw new NullReferenceException("No Task<T> result was found. This is unexpected and may indicate an internal issue. Verify the RequestResponseHandler implementation.");
                            }
                            
                            break;

                        case RqConsumerPurpose.RRResponseConsumer:
                            await InvokeAsync(consumeMethod.Invoke(handlerInstance, new object?[] { modelInstance, correlationId }));
                            break;

                        default:
                            throw new NotSupportedException("No consumer found for the specified handler.");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while processing the message in UniversalConsumer.");
                    await ErrorAsync(
                        ex,
                        new ErrorConsumerDetail(
                            modelInstance,
                            bodyBytes,
                            name,
                            record?.supportedInterfacesType?.FullName,
                            record?.ConcreteHandlerInterfaceType?.FullName,
                            record?.HandlerType?.FullName,
                            record?.GenericType?.FullName,
                            ErrorConsumerType.RecevingProblem));
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error occurred while processing the message in UniversalConsumer.");
                await ErrorAsync(
                    e,
                    new ErrorConsumerDetail(
                        null,
                        null,
                        name,
                        record?.supportedInterfacesType?.FullName,
                        record?.ConcreteHandlerInterfaceType?.FullName,
                        record?.HandlerType?.FullName,
                        record?.GenericType?.FullName,
                        ErrorConsumerType.RecevingProblem));
            }
        }
        private static async Task InvokeAsync(object? invokeResult)
        {
            if (invokeResult is Task task)
            {
                await task;
            }
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
                logger.LogCritical(handlerException, "An exception occurred while handling Error. Check your error handlers.");
            }
        }
    }
}
