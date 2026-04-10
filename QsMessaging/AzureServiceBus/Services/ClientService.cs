using Azure.Messaging.ServiceBus;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;

namespace QsMessaging.AzureServiceBus.Services
{
    internal class ClientService(IQsMessagingConfiguration configuration) : IClientService, IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private ServiceBusClient? _client;

        public async Task<ServiceBusClient> GetOrCreateClientAsync(CancellationToken cancellationToken = default)
        {
            if (_client is not null)
            {
                return _client;
            }

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _client ??= new ServiceBusClient(configuration.AzureServiceBus.ConnectionString);
                return _client;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public bool IsInitialized()
        {
            return _client is not null;
        }

        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_client is null)
                {
                    return;
                }

                await _client.DisposeAsync();
                _client = null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await CloseAsync();
            _semaphore.Dispose();
        }
    }
}
