using QsMessaging.Public;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq
{
    internal class RabbitMqConnectionStorage(QsMessagingConfiguration configuration) : IRabbitMqConnectionStorage
    {
        private IConnection _connection;

        public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            while (_connection == null || !_connection.IsOpen)
            {
                _connection = await CreateConnectionAsync();
                //TODO: Implement logariphmick waiting strategy
                await Task.Delay(1000).WaitAsync(cancellationToken);
            }

            return _connection;
        }

        private async Task<IConnection> CreateConnectionAsync()
        {
            var factory = new ConnectionFactory()
            {
                HostName = configuration.Host,
                UserName = configuration.UserName,
                Password = configuration.Password,
                Port = configuration.Port
            };

            return await factory.CreateConnectionAsync();
        }
    }
}
