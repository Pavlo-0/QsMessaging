using QsMessaging.Public;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq.Services
{
    internal class ConnectionManager(IConnectionWorker connectionWorker) : IQsMessagingConnectionManager
    {
        public async Task Close()
        {
            var connection = connectionWorker.GetConnection().connection;
            if (IsConnected(connection))
            {
                await connection.CloseAsync(200, "Normal shutdown by request");
            }
        }

        public bool IsConnected()
        {
            
            return IsConnected(connectionWorker.GetConnection().connection);
        }

        public async Task Open()
        {
            await connectionWorker.GetOrCreateConnectionAsync();
        }

        private bool IsConnected(IConnection? connection)
        {
            return connection is not null && connection.IsOpen;
        }
    }
}
