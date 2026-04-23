namespace QsMessaging.RabbitMq.Interfaces
{
    internal interface ISender
    {
        public Task SendMessageAsync<TMessage>(TMessage model) where TMessage : class;

        public Task SendEventAsync<TEvent>(TEvent model) where TEvent : class;

        Task<TResponse> SendRequest<TRequest, TResponse>(TRequest model, CancellationToken cancellationToken) where TRequest : class where TResponse : class;

        internal Task SendMessageCorrelatedAsync(
            object model,
            string correlationId,
            string replyTo,
            CancellationToken cancellationToken = default);
    }
}
