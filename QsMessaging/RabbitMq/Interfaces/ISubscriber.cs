using QsMessaging.RabbitMq.Models;

namespace QsMessaging.RabbitMq.Interface
{
    internal interface ISubscriber
    {
        Task SubscribeAsync();
        Task SubscribeHandlerAsync(HandlersStoreRecord record);
    }
}