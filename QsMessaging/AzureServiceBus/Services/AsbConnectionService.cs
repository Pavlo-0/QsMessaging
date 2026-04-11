using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;

namespace QsMessaging.AzureServiceBus.Services
{
    internal class AsbConnectionService(
        ILogger<AsbConnectionService> logger,
        IQsMessagingConfiguration configuration) : IConnectionService, IAsyncDisposable
    {
        private static ServiceBusClient? connection;
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
