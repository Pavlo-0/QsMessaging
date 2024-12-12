using Microsoft.Extensions.Logging;
using QsMessaging.Public;
using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;

namespace QsMessaging.RabbitMq
{
    internal class ConnectionManager(
        ILogger<ConnectionManager> logger,
        IConnectionService connectionWorker, 
        ISubscriber subscriber) : IQsMessagingConnectionManager
    {
        public async Task Close(CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Closing connection to RabbitMQ.");
            var conn = connectionWorker.GetConnection();
            if (conn is null)
            {
                return;
            }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            conn.CloseAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            await conn.DisposeAsync();

            do
            {
                await Task.Delay(10);
            } while (conn != null && conn.IsOpen && !cancellationToken.IsCancellationRequested);
        }

        public bool IsConnected()
        {
            return IsConnected(connectionWorker.GetConnection());
        }

        public async Task Open()
        {
            logger.LogInformation("Opening connection to RabbitMQ.");
            await subscriber.SubscribeAsync();
        }

        private bool IsConnected(IConnection? connection)
        {
            return connection is not null && connection.IsOpen;
        }
    }
}
