using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.Transporting.Interfaces;

namespace QsMessaging.Transporting
{
    internal class RabbitMqTransportSenderAdapter(IRabbitMqSender sender) : ITransportSender
    {
        public Task SendMessageAsync<TMessage>(TMessage model) where TMessage : class
        {
            return sender.SendMessageAsync(model);
        }

        public Task SendEventAsync<TEvent>(TEvent model) where TEvent : class
        {
            return sender.SendEventAsync(model);
        }

        public Task<TResponse> SendRequest<TRequest, TResponse>(TRequest model, CancellationToken cancellationToken)
            where TRequest : class
            where TResponse : class
        {
            return sender.SendRequest<TRequest, TResponse>(model, cancellationToken);
        }
    }
}
