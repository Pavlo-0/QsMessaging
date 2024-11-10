
namespace QsMessaging.Services.Interfaces
{
    internal interface IRabbitMqSender
    {
        public Task<bool> SendMessageAsync<TMessage>(TMessage model);
    }
}
