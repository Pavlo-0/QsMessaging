using QsMessaging.Public;
using QsMessaging.RabbitMq.Interfaces;

namespace QsMessaging
{
    internal class QsMessagingGate(IRabbitMqSender rabbitMqSender) : IQsMessaging
    {
        public Task<bool> SendMessageAsync<TMessage>(TMessage model) where TMessage : class
        {
            return rabbitMqSender.SendMessageAsync(model);
        }

        public Task<bool> SendEventAsync<TEvent>(TEvent model) where TEvent : class
        {
            return rabbitMqSender.SendEventAsync(model);
        }
    }
}
