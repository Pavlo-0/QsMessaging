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

        public Task<TResponse> RequestResponse<TRequest, TResponse>(TRequest request) where TRequest : class where TResponse : class
        {

            throw new NotImplementedException();
            //return rabbitMqSender.RequestResponse<TRequest, TResponse>(request);
        }

    }
}
