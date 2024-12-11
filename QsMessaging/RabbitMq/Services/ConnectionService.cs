using Microsoft.Extensions.Logging;
using QsMessaging.Public;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services
{
    internal class ConnectionService(ILogger<ConnectionService> logger, IQsMessagingConfiguration configuration) : IConnectionService
    {
        private static IConnection? connection;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public IConnection? GetConnection()
        {
            return connection;
        }

        public async Task<IConnection> GetOrCreateConnectionAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync();
            try
            {
                int attempt = 1;
                if (connection != null && connection.IsOpen)
                {
                    return connection;
                }

                logger.LogInformation("Attempting to connect to RabbitMQ");

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        connection = await CreateConnectionAsync(cancellationToken);

                        if (connection != null && connection.IsOpen)
                        {
                            logger.LogInformation("Connection to RabbitMQ established");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the exception
                        logger.LogWarning($"Connection Attempt {attempt}: {ex.Message}");
                    }

                    // Implementing logarithmic waiting strategy untill 35 sec
                    int waitTime = attempt < 10 ? (int)(Math.Pow(2, attempt) * 100) : 30 * 1000;
                    try
                    {
                        await Task.Delay(waitTime, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        // Task was canceled
                        logger.LogWarning("Task was canceled");
                        break;
                    }

                    attempt++;
                }
            }
            finally
            {
                _semaphore.Release();
            }

            //Cancelation requested
            cancellationToken.ThrowIfCancellationRequested();
#pragma warning disable CS8603 // Possible null reference return.
            return null;
#pragma warning restore CS8603 // Possible null reference return.
        }

        private async Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
        {
            var factory = new ConnectionFactory()
            {
                HostName = configuration.RabbitMQ.Host,
                UserName = configuration.RabbitMQ.UserName,
                Password = configuration.RabbitMQ.Password,
                Port = configuration.RabbitMQ.Port,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };

            logger.LogDebug("{Host}:{Port}", factory.HostName, factory.Port);

            return await factory.CreateConnectionAsync(configuration.ServiceName, cancellationToken);
        }
    }
}
