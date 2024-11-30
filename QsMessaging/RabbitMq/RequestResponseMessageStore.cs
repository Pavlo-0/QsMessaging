using System.Collections.Concurrent;

namespace QsMessaging.RabbitMq.Interfaces
{
    internal class RequestResponseMessageStore: IRequestResponseMessageStore
    {
        private static ConcurrentDictionary<string, StoreMessageRecord> storeConsumerRecords = new ConcurrentDictionary<string, StoreMessageRecord>();

        private record StoreMessageRecord(object RequestMessage, Type RequestMessageType, object? ResponseMessage, Type? ResponseMessageType, bool IsResponsed, DateTime CreateDate );

        public void AddRequestMessage(string correlationId, object message)
        {
            var record = new StoreMessageRecord(message, message.GetType(), null, null, false, DateTime.UtcNow);
            storeConsumerRecords[correlationId] = record;
        }

        /// <summary>
        /// Marks a message as responded.
        /// </summary>
        /// <param name="correlationId">Unique identifier of the message to mark as responded.</param>
        public void MarkAsResponded(string correlationId, object message)
        {
            if (storeConsumerRecords.TryGetValue(correlationId, out var record))
            {
                // Update the record with IsResponsed set to true
                var updatedRecord = record with { 
                    IsResponsed = true, 
                    ResponseMessage = message, 
                    ResponseMessageType = message.GetType() };
                storeConsumerRecords[correlationId] = updatedRecord;
            }
            else
            {
                throw new KeyNotFoundException($"Message with ID {correlationId} not found.");
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
        public (object message, Type messageType ) GetRespondedMessage(string correlationId)
        {
            if (storeConsumerRecords.TryGetValue(correlationId, out var record) && 
                record.ResponseMessage is not null && 
                record.ResponseMessageType is not null)
            {
                return (record.ResponseMessage, record.ResponseMessageType);
            }
            throw new KeyNotFoundException($"Message with ID {correlationId} not found.");
        }

        /// <summary>
        /// Removes a message from the store.
        /// </summary>
        /// <param name="correlationId">The correlation ID of the message to remove.</param>
        public void RemoveMessage(string correlationId)
        {
            storeConsumerRecords.TryRemove(correlationId, out _);
        }
    }

}
