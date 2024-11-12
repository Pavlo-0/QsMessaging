using QsMessaging.RabbitMq.Interface;
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
            var (connection, channel) = await rabbitMqConnectionStorage.GetConnectionAsync();

            // Declare an exchange of type 'fanout'
            string exchangeName = exchangeNameGenerator.GetExchangeNameFromType<TMessage>();
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
