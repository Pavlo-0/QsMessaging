using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using QsMessaging.Public;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Models;

namespace QsMessaging.Shared
{
    internal class RequestResponseMessageStore(ILogger<RequestResponseMessageStore> logger, IQsMessagingConfiguration config): IRequestResponseMessageStore
    {
        private static ConcurrentDictionary<string, StoreMessageRecord> storeConsumerRecords = new ConcurrentDictionary<string, StoreMessageRecord>();

        //Return task for wait response
        public Task AddRequestMessageAsync(string correlationId, object message, CancellationToken cancellationToken)
        {
            logger.LogTrace("Request message added with Correlation ID: {CorrelationId}.", correlationId);

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var timeoutCancellation = new CancellationTokenSource();

            var record = new StoreMessageRecord(message, message.GetType(), null, null, false, DateTime.UtcNow, tcs);
            storeConsumerRecords[correlationId] = record;

            _ = CompleteOnTimeoutAsync(correlationId, tcs, timeoutCancellation.Token);

            var cancellationRegistration = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(
                    static state =>
                    {
                        var cancellationState = (RequestCancellationState)state!;
                        if (cancellationState.TaskCompletionSource.TrySetCanceled(cancellationState.CancellationToken))
                        {
                            storeConsumerRecords.TryRemove(cancellationState.CorrelationId, out _);
                        }
                    },
                    new RequestCancellationState(correlationId, tcs, cancellationToken))
                : default;

            _ = tcs.Task.ContinueWith(
                static (_, state) =>
                {
                    var cleanupState = (RequestCleanupState)state!;
                    cleanupState.CancellationRegistration.Dispose();
                    cleanupState.TimeoutCancellation.Cancel();
                    cleanupState.TimeoutCancellation.Dispose();
                },
                new RequestCleanupState(timeoutCancellation, cancellationRegistration),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            return tcs.Task;
        }

        private async Task CompleteOnTimeoutAsync(
            string correlationId,
            TaskCompletionSource<bool> taskCompletionSource,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(config.RequestResponseTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (taskCompletionSource.TrySetException(new TimeoutException("Request timed out")))
            {
                logger.LogTrace("Request message timed out. Correlation ID: {CorrelationId}.", correlationId);
                storeConsumerRecords.TryRemove(correlationId, out _);
            }
        }

        /// <summary>
        /// Marks a message as responded.
        /// </summary>
        /// <param name="correlationId">Unique identifier of the message to mark as responded.</param>
        public void MarkAsResponded(string correlationId, object message)
        {
            logger.LogTrace("Message marked as responded. Correlation ID: {CorrelationId}.", correlationId);

            while (storeConsumerRecords.TryGetValue(correlationId, out var record))
            {
                if (record.IsResponded)
                {
                    return;
                }

                var updatedRecord = record with { 
                    IsResponded = true, 
                    ResponseMessage = message, 
                    ResponseMessageType = message.GetType() };

                if (!storeConsumerRecords.TryUpdate(correlationId, updatedRecord, record))
                {
                    continue;
                }

                if (!record.task.TrySetResult(true) && !record.task.Task.IsCompletedSuccessfully)
                {
                    storeConsumerRecords.TryRemove(correlationId, out _);
                }

                return;
            }

            logger.LogTrace("Message with ID {CorrelationId} ignored because it is not intended for this node", correlationId);
        }

        public bool IsRespondedMessage(string correlationId)
        {
            if (storeConsumerRecords.TryGetValue(correlationId, out var record))
            {
                return record.IsResponded;
            }
            throw new KeyNotFoundException($"Message with ID {correlationId} not found.");
        }

        /// <summary>
        /// Retrieves a message by its correlation ID.
        /// </summary>
        /// <param name="correlationId">The correlation ID of the message.</param>
        /// <returns>The message object if found, or null otherwise.</returns>
        public TResponse GetRespondedMessage<TResponse>(string correlationId)
        {
            if (storeConsumerRecords.TryGetValue(correlationId, out var record) && 
                record.ResponseMessage is not null && 
                record.ResponseMessageType is not null)
            {
                return (TResponse)record.ResponseMessage;
            }
            throw new KeyNotFoundException($"Message with ID {correlationId} not found.");
        }

        /// <summary>
        /// Removes a message from the store.
        /// </summary>
        /// <param name="correlationId">The correlation ID of the message to remove.</param>
        public void RemoveMessage(string correlationId)
        {
            logger.LogTrace("Message removed from the store. Correlation ID: {CorrelationId}.", correlationId);
            if (storeConsumerRecords.TryRemove(correlationId, out var record))
            {
                record.task.TrySetCanceled();
            }
        }

        private sealed record RequestCancellationState(
            string CorrelationId,
            TaskCompletionSource<bool> TaskCompletionSource,
            CancellationToken CancellationToken);

        private sealed record RequestCleanupState(
            CancellationTokenSource TimeoutCancellation,
            CancellationTokenRegistration CancellationRegistration);
    }
}
