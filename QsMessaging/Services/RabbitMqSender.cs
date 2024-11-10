using QsMessaging.Services.Interfaces;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace QsMessaging.Services
{
    internal class RabbitMqSender : IRabbitMqSender
    {
        public RabbitMqSender()
        {
        }

        public async Task<bool> SendMessageAsync<TMessage>(TMessage model)
        {
            var factory = new ConnectionFactory()
            {
                HostName = "localhost",
                UserName = "guest", // Default user
                Password = "guest", // Default password
                Port = 5672
            };

            using var connection = await factory.CreateConnectionAsync();
            // Create a channel within the connection
            using var channel = await connection.CreateChannelAsync();

            // Declare an exchange of type 'fanout'
            string exchangeName = "test_fanout_exchange";
            await channel.ExchangeDeclareAsync(
                exchange: exchangeName,
                type: ExchangeType.Fanout,
                durable: true,
                autoDelete: false,
                arguments: null);

            var jsonMessage = JsonSerializer.Serialize(model);
            var body = Encoding.UTF8.GetBytes(jsonMessage);

            await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: string.Empty,  // No routing key needed for fanout
            body: body);

            return true;
        }
    }
}
