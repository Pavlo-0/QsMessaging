namespace QsMessaging.RabbitMq.Interfaces
{
    internal interface IRabbitMqSender
    {
        public Task<bool> SendMessageAsync<TMessage>(TMessage model) where TMessage : class;

        public Task<bool> SendEventAsync<TEvent>(TEvent model) where TEvent : class;
    }
}
