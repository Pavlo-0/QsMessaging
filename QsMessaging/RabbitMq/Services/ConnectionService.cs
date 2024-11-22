using QsMessaging.Public;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services
{
    internal class ConnectionService(QsMessagingConfiguration configuration) : IConnectionService
    {
        private readonly static object _lock = new object();
        private static IConnection? _lockValueConnection;

        private static IConnection? connection
        {
            get
            {
                lock (_lock)
                {
                    return _lockValueConnection;
                }
            }
            set
            {
                lock (_lock)
                {
                    _lockValueConnection = value;
                }
            }
        }
      
        public IConnection? GetConnection()
        {
            return connection;
        }

        public async Task<IConnection> GetOrCreateConnectionAsync(CancellationToken cancellationToken)
        {
            var attempt = 0;
            if (connection != null && connection.IsOpen)
            {
                return connection;
            }

            do
            {
                connection = await CreateConnectionAsync();
                //TODO: Implement logariphmick waiting strategy
                await Task.Delay(attempt + 1000).WaitAsync(cancellationToken);
                attempt++;
            }
            while (!(connection != null && connection.IsOpen));

            return connection;
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

            return await factory.CreateConnectionAsync();
        }
    }
}
