namespace QsMessaging.RabbitMq.Interfaces
{
    internal interface IRequestResponseMessageStore
    {
        void AddRequestMessage(string correlationId, object message);
        void MarkAsResponded(string correlationId, object message);
        bool IsRespondedMessage(string correlationId);
        (object message, Type messageType) GetRespondedMessage(string correlationId);
        void RemoveMessage(string correlationId);
    }

}
