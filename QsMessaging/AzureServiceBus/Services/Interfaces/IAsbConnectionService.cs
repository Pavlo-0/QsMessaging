using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace QsMessaging.AzureServiceBus.Services.Interfaces
{
    internal interface IAsbConnectionService
    {
        ServiceBusClient? GetConnection();

        Task<ServiceBusClient> GetOrCreateConnectionAsync(CancellationToken cancellationToken = default);

        Task CloseAsync(CancellationToken cancellationToken = default);

        ServiceBusAdministrationClient? GetAdministrationClient();

        Task<ServiceBusAdministrationClient> GetOrCreateAdministrationClientAsync(CancellationToken cancellationToken = default);

        Task CloseAdministrationClientAsync(CancellationToken cancellationToken = default);
    }
}
