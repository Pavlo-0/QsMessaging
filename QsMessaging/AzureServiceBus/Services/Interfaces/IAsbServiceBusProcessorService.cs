using QsMessaging.Shared.Models;

namespace QsMessaging.AzureServiceBus.Services
{
    internal interface IAsbServiceBusProcessorService
    {
        Task<AsbProcessorRegistration> GetOrCreate(HandlersStoreRecord record, CancellationToken cancellationToken);
    }
}
