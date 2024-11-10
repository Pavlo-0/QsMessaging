using QsMessaging.Public;
using QsMessaging.RabbitMq.Services.Interfaces;

namespace QsMessaging.Services
{
    internal class QsMessagingGate(IRabbitMqSender rabbitMqSender) : IQsMessaging
    {
        public Task<bool> SendEventAsync<TEvent>(TEvent Model)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SendMessageAsync<TMessage>(TMessage model)
        {
            return rabbitMqSender.SendMessageAsync(model);
        }
    }
}
