using QsMessaging.RabbitMq.Models.Enums;

namespace QsMessaging.AzureServiceBus.Services.Interfaces
{
    internal interface IQueueAdministration
    {
        Task<string> GetOrCreateQueueAsync(Type contractType, QueuePurpose purpose, CancellationToken cancellationToken = default);
    }
}