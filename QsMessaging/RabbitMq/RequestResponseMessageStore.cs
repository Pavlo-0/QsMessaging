using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using QsMessaging.Public;
using QsMessaging.RabbitMq.Models;

namespace QsMessaging.RabbitMq.Interfaces
{
    internal class RequestResponseMessageStore(ILogger<RequestResponseMessageStore> logger, IQsMessagingConfiguration config): IRequestResponseMessageStore
    {
        private static ConcurrentDictionary<string, StoreMessageRecord> storeConsumerRecords = new ConcurrentDictionary<string, StoreMessageRecord>();

        //Return task for wait response
        public Task AddRequestMessageAsync(string correlationId, object message, CancellationToken cancellationToken)
        {
            logger.LogTrace("Request message added with Correlation ID: {CorrelationId}.", correlationId);


            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var record = new StoreMessageRecord(message, message.GetType(), null, null, false, DateTime.UtcNow, tcs);
            storeConsumerRecords[correlationId] = record;

            // Create a timeout task
            var timeoutTask = Task.Delay(config.RequestResponseTimeout, cancellationToken)
                .ContinueWith(_ => tcs.TrySetException(new TimeoutException("Request timed out")),
                              cancellationToken,
                              TaskContinuationOptions.ExecuteSynchronously,
                              TaskScheduler.Default);

            return tcs.Task;
        }

        /// <summary>
        /// Marks a message as responded.
        /// </summary>
        /// <param name="correlationId">Unique identifier of the message to mark as responded.</param>
        public void MarkAsResponded(string correlationId, object message)
        {
            logger.LogTrace("Message marked as responded. Correlation ID: {CorrelationId}.", correlationId);

            if (storeConsumerRecords.TryGetValue(correlationId, out var record))
            {
                // Update the record with IsResponsed set to true
                var updatedRecord = record with { 
                    IsResponsed = true, 
                    ResponseMessage = message, 
                    ResponseMessageType = message.GetType() };
                storeConsumerRecords[correlationId] = updatedRecord;
                record.task.TrySetResult(true);
            }
            else
            {
                logger.LogTrace("Message with ID {CorrelationId} ignored because it is not intended for this node", correlationId);
            }
        }

        public bool IsRespondedMessage(string correlationId)
        {
            if (storeConsumerRecords.TryGetValue(correlationId, out var record))
            {
                return record.IsResponsed;
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
            storeConsumerRecords.TryRemove(correlationId, out _);
        }
    }
}
