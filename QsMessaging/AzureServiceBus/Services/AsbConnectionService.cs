using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using Azure.Messaging.ServiceBus.Administration;
using QsMessaging.Public;

namespace QsMessaging.AzureServiceBus.Services
{
    internal class AsbConnectionService(
        ILogger<AsbConnectionService> logger,
        IQsMessagingConfiguration configuration) : IAsbConnectionService, IAsyncDisposable
    {
        private static ServiceBusClient? connection;
        private static ServiceBusAdministrationClient? administrationClient;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public ServiceBusClient? GetConnection()
        {
            return connection;
        }

        public async Task<ServiceBusClient> GetOrCreateConnectionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (connection is not null && !connection.IsClosed)
                {
                    return connection;
                }

                connection = CreateConnection(cancellationToken);
                logger.LogInformation("Azure Service Bus client created for {Endpoint}", GetEndpoint(configuration.AzureServiceBus));
                return connection;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            ServiceBusClient? connectionToDispose = null;

            cancellationToken.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (connection is null || connection.IsClosed)
                {
                    connection = null;
                    return;
                }

                connectionToDispose = connection;
                connection = null;
            }
            finally
            {
                _semaphore.Release();
            }

            await connectionToDispose.DisposeAsync();
        }

        public ServiceBusAdministrationClient? GetAdministrationClient()
        {
            return administrationClient;
        }

        public async Task<ServiceBusAdministrationClient> GetOrCreateAdministrationClientAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (administrationClient is not null)
                {
                    return administrationClient;
                }

                administrationClient = new ServiceBusAdministrationClient(
                    ConnectionStringHelper.GetAdministrationConnectionString(configuration.AzureServiceBus));
                logger.LogInformation("Azure Service Bus administration client created for {Endpoint}", GetEndpoint(configuration.AzureServiceBus));
                return administrationClient;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task CloseAdministrationClientAsync(CancellationToken cancellationToken = default)
        {
            ServiceBusAdministrationClient? adminToDispose = null;

            cancellationToken.ThrowIfCancellationRequested();
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (administrationClient is null)
                {
                    administrationClient = null;
                    return;
                }

                adminToDispose = administrationClient;
                administrationClient = null;
            }
            finally
            {
                _semaphore.Release();
            }

            switch (adminToDispose)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await CloseAsync();
            _semaphore.Dispose();
        }

        private ServiceBusClient CreateConnection(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string connectionString = ConnectionStringHelper.GetClientConnectionString(configuration.AzureServiceBus);
            return new ServiceBusClient(connectionString);
        }

        private static string GetEndpoint(QsAzureServiceBusConfiguration configuration)
        {
            return ConnectionStringHelper.GetClientConnectionString(configuration)
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(part => part.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase))
                ?? "unknown";
        }
    }
}
