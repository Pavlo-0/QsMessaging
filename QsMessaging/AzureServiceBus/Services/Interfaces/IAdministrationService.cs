using QsMessaging.RabbitMq.Models;
using QsMessaging.RabbitMq.Services;

namespace QsMessaging.AzureServiceBus.Services.Interfaces
{
    internal interface IAdministrationService
    {
        Task<string> GetOrCreateQueueAsync(Type contractType, QueuePurpose purpose, CancellationToken cancellationToken = default);
        string GetQueueName(Type contractType, QueuePurpose purpose);
        Task<bool> QueueExistsAsync(string queueName, CancellationToken cancellationToken = default);
        Task<string> GetOrCreateTopicAsync(Type contractType, CancellationToken cancellationToken = default);
        Task<string> GetOrCreateSubscriptionAsync(HandlersStoreRecord record, CancellationToken cancellationToken = default);
        Task DeleteOwnedEntitiesAsync(CancellationToken cancellationToken = default);
    }
}
