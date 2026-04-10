namespace QsMessaging.Transporting.Interfaces
{
    internal interface ITransportSender
    {
        Task SendMessageAsync<TMessage>(TMessage model) where TMessage : class;

        Task SendEventAsync<TEvent>(TEvent model) where TEvent : class;

        Task<TResponse> SendRequest<TRequest, TResponse>(TRequest model, CancellationToken cancellationToken)
            where TRequest : class
            where TResponse : class;
    }
}
