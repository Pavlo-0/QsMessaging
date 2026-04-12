using QsMessaging.RabbitMq.Models;

namespace QsMessaging.Shared.Interface
{
    internal interface ISubscriber
    {
        Task SubscribeAsync(CancellationToken cancellationToken = default);
        Task SubscribeHandlerAsync(HandlersStoreRecord record, CancellationToken cancellationToken = default);
        Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
