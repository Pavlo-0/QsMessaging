using QsMessaging.Public;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services
{
    internal class ConnectionService(QsMessagingConfiguration configuration) : IConnectionService
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
                var attempt = 0;
                if (connection != null && connection.IsOpen)
                {
                    return connection;
                }

                do
                {
                    try
                    {
                        connection = await CreateConnectionAsync();
                    }
                    catch (Exception ex)
                    {
                        //Log exception
                    }

                    //TODO: Implement logariphmick waiting strategy
                    await Task.Delay(attempt + 1000).WaitAsync(cancellationToken);
                    attempt++;
                }
                while (!(connection != null && connection.IsOpen));
                //TODO: Add cancelation request

                return connection;

            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<IConnection> CreateConnectionAsync()
        {
            var factory = new ConnectionFactory()
            {
                HostName = configuration.RabbitMQ.Host,
                UserName = configuration.RabbitMQ.UserName,
                Password = configuration.RabbitMQ.Password,
                Port = configuration.RabbitMQ.Port
            };

            return await factory.CreateConnectionAsync(configuration.ServiceName);
        }
    }
}
