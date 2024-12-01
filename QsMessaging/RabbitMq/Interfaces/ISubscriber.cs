using QsMessaging.RabbitMq.Services;

namespace QsMessaging.RabbitMq.Interface
{
    internal interface ISubscriber
    {
        Task Subscribe();
        Task SubscribeHandlerAsync(HandlerService.HandlersStoreRecord record);
    }
}