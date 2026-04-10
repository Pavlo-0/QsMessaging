using QsMessaging.RabbitMq.Models;

namespace QsMessaging.AzureServiceBus
{
    internal interface IAzureServiceBusSubscriber
    {
        Task SubscribeAsync(CancellationToken cancellationToken = default);
        Task SubscribeHandlerAsync(HandlersStoreRecord record, CancellationToken cancellationToken = default);
        Task CloseAsync(CancellationToken cancellationToken = default);
    }
}
