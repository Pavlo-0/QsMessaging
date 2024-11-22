using QsMessaging.Public;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;
using System.Threading.Channels;

namespace QsMessaging.RabbitMq
{
    internal class ConnectionManager(IConnectionService connectionWorker, IChannelService channelGenerator) : IQsMessagingConnectionManager
    {
        public async Task Close()
        {
            var conn = connectionWorker.GetConnection();
            if (conn is null)
            {
                return;
            }

            var channels = channelGenerator.GetByConnection(conn);

            //var (conn, channel) = connectionWorker.GetConnection();
            /*
            await conn.CloseAsync();
            await channel.CloseAsync();
            await channel.DisposeAsync();
            await conn.DisposeAsync();*/
        }

        public bool IsConnected()
        {

            return IsConnected(connectionWorker.GetConnection());
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
