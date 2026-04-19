using QsMessaging.AzureServiceBus.Models.Enums;
using QsMessaging.RabbitMq.Models.Enums;

namespace QsMessaging.AzureServiceBus.Services.Interfaces
{
    internal interface IAsbQueueService
    {
        Task<string> GetOrCreateQueueAsync(Type contractType, AsbQueuePurpose queuePurpose, CancellationToken cancellationToken = default);
    }
}