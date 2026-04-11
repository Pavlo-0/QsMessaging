using Azure.Messaging.ServiceBus;

namespace QsMessaging.AzureServiceBus.Services.Interfaces
{
    internal interface IConnectionService
    {
        ServiceBusClient? GetConnection();

        Task<ServiceBusClient> GetOrCreateConnectionAsync(CancellationToken cancellationToken = default);

        Task CloseAsync(CancellationToken cancellationToken = default);
    }
}
