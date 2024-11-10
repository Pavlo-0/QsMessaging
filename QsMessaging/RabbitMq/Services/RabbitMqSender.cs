using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Services.Interfaces;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace QsMessaging.RabbitMq.Services
{
    internal class RabbitMqSender(
        IRabbitMqConnectionStorage rabbitMqConnectionStorage, 
        IExchangeNameGenerator exchangeNameGenerator) : IRabbitMqSender
    {
        public async Task<bool> SendMessageAsync<TMessage>(TMessage model)
        {
           

            var connection = await rabbitMqConnectionStorage.GetConnectionAsync();
            // Create a channel within the connection
            using var channel = await connection.CreateChannelAsync();

            // Declare an exchange of type 'fanout'
            string exchangeName = exchangeNameGenerator.GetNameFromType<TMessage>();
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
