using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;

namespace QsMessaging.AzureServiceBus
{
    internal class ConnectionManager(
        ILogger<ConnectionManager> logger,
        IClientService clientService,
        IAdministrationService administrationService,
        IAzureServiceBusSubscriber subscriber) : IQsMessagingConnectionManager
    {
        public async Task Close(CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Closing Azure Service Bus transport.");
            await subscriber.CloseAsync(cancellationToken);
            await administrationService.DeleteOwnedEntitiesAsync(cancellationToken);
            await clientService.CloseAsync(cancellationToken);
        }

        public bool IsConnected()
        {
            return clientService.IsInitialized();
        }

        public async Task Open()
        {
            logger.LogInformation("Opening Azure Service Bus transport.");
            await subscriber.SubscribeAsync();
        }
    }
}
