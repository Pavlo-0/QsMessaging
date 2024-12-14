namespace QsMessaging.RabbitMq.Interfaces
{
    internal interface IRequestResponseMessageStore
    {
        Task AddRequestMessageAsync(string correlationId, object message, CancellationToken cancellationToken);
        void MarkAsResponded(string correlationId, object message);
        bool IsRespondedMessage(string correlationId);
        TResponse GetRespondedMessage<TResponse>(string correlationId);
        void RemoveMessage(string correlationId);
    }

}
