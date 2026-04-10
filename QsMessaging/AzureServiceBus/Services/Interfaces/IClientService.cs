using Azure.Messaging.ServiceBus;

namespace QsMessaging.AzureServiceBus.Services.Interfaces
{
    internal interface IClientService
    {
        Task<ServiceBusClient> GetOrCreateClientAsync(CancellationToken cancellationToken = default);
        bool IsInitialized();
        Task CloseAsync(CancellationToken cancellationToken = default);
    }
}
