using QsMessaging.Public;
using QsMessaging.RabbitMq.Interfaces;
using System.Reflection;

namespace QsMessaging
{
    internal class QsMessagingGate(IRabbitMqSender rabbitMqSender) : IQsMessaging
    {
        public Task<bool> SendEventAsync<TEvent>(TEvent model)
        {
            return rabbitMqSender.SendEventAsync(model);
        }

        public Task<bool> SendMessageAsync<TMessage>(TMessage model)
        {
            return rabbitMqSender.SendMessageAsync(model);
        }
    }
}
