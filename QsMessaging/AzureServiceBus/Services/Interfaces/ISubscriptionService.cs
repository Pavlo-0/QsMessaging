using QsMessaging.RabbitMq.Models;

namespace QsMessaging.AzureServiceBus.Services.Interfaces
{
    internal interface ISubscriptionService
    {
        Task<string> GetOrCreateSubscriptionAsync(HandlersStoreRecord record, CancellationToken cancellationToken = default);
    }
}
