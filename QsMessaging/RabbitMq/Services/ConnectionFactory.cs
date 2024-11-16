using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services
{
    internal class ConnectionFactory
    {
        // Give me method which establish connection with RabbitMq instance and return connection. Parameters for this connection make as parameter function
        public async Task<IConnection> CreateConnectionAsync(Func<RabbitMQ.Client.ConnectionFactory, RabbitMQ.Client.ConnectionFactory> connectionFactory)
        {
            var factory = connectionFactory(new RabbitMQ.Client.ConnectionFactory());
            return await factory.CreateConnectionAsync();
        }
    }

}
