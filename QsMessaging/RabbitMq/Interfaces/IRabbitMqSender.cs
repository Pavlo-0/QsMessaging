namespace QsMessaging.RabbitMq.Interfaces
{
    internal interface IRabbitMqSender
    {
        public Task SendMessageAsync<TMessage>(TMessage model) where TMessage : class;

        public Task SendEventAsync<TEvent>(TEvent model) where TEvent : class;

        Task<TResponse> SendRequest<TRequest, TResponse>(TRequest model, CancellationToken cancellationToken) where TRequest : class where TResponse : class;
    }
}
