using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QsMessaging.RabbitMq.Services
{
    internal class RabbitMqConnectionFactory
    {
        // Give me method which establish connection with RabbitMq instance and return connection. Parameters for this connection make as parameter function
        public async Task<IConnection> CreateConnectionAsync(Func<ConnectionFactory, ConnectionFactory> connectionFactory)
        {
            var factory = connectionFactory(new ConnectionFactory());
            return await factory.CreateConnectionAsync();
        }


    }
}
