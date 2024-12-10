using QsMessaging.Public;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services
{
    internal class ConnectionService(IQsMessagingConfiguration configuration) : IConnectionService
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
                int attempt = 0;
                if (connection != null && connection.IsOpen)
                {
                    return connection;
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        connection = await CreateConnectionAsync(cancellationToken);

                        if (connection != null && connection.IsOpen)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the exception
                        Console.WriteLine($"Attempt {attempt}: {ex.Message}");
                    }

                    // Implementing logarithmic waiting strategy
                    int waitTime = (int)(Math.Pow(2, attempt) * 100); // e.g., exponential backoff with a base of 2
                    waitTime = Math.Min(waitTime, 10000); // Cap the wait time to a maximum (e.g., 10 seconds)

                    try
                    {
                        await Task.Delay(waitTime, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        // Task was canceled
                        break;
                    }

                    attempt++;
                }

                if (connection == null || !connection.IsOpen)
                {
                    throw new InvalidOperationException("Failed to establish a connection after multiple attempts.");
                }

                return connection;

            }
            finally
            {
                _semaphore.Release();
            }
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

            return await factory.CreateConnectionAsync(configuration.ServiceName, cancellationToken);
        }
    }
}
