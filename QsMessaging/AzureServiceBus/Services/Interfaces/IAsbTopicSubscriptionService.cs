using QsMessaging.Shared.Models;

namespace QsMessaging.AzureServiceBus.Services.Interfaces
{
    internal interface IAsbTopicSubscriptionService
    {
        Task<string> GetOrCreateSubscriptionAsync(HandlersStoreRecord record, CancellationToken cancellationToken = default);
        Task DeleteTemporarySubscriptionsAsync(CancellationToken cancellationToken = default);
    }
}
