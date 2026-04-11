using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;
using QsMessaging.Shared.Interface;
using AzureConnectionService = QsMessaging.AzureServiceBus.Services.Interfaces.IConnectionService;

namespace QsMessaging.AzureServiceBus
{
    internal class AsbConnectionManager(
        ILogger<AsbConnectionManager> logger,
        AzureConnectionService connectionWorker,
        IAdministrationService administrationService,
        ISubscriber subscriber) : IQsMessagingConnectionManager
    {
        private readonly SemaphoreSlim _lifecycleSemaphore = new(1, 1);

        public async Task Close(CancellationToken cancellationToken = default)
        {
            await _lifecycleSemaphore.WaitAsync(cancellationToken);
            try
            {
                logger.LogInformation("Closing Azure Service Bus transport.");
                await subscriber.CloseAsync(cancellationToken);
                try
                {
                    await administrationService.DeleteOwnedEntitiesAsync(cancellationToken);
                }
                finally
                {
                    await connectionWorker.CloseAsync(cancellationToken);
                }
            }
            finally
            {
                _lifecycleSemaphore.Release();
            }
        }

        public bool IsConnected()
        {
            return connectionWorker.GetConnection() is { IsClosed: false };
        }

        public async Task Open()
        {
            await _lifecycleSemaphore.WaitAsync();
            try
            {
                logger.LogInformation("Opening Azure Service Bus transport.");
                await subscriber.SubscribeAsync();
            }
            finally
            {
                _lifecycleSemaphore.Release();
            }
        }
    }
}
