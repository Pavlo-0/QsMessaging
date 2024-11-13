using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace QsMessaging.RabbitMq.Services
{
    internal class Sender(
        IRabbitMqConnectionStorage rabbitMqConnectionStorage, 
        IExchangeNameGenerator exchangeNameGenerator,
        IQueuesGenerator queuesGenerator) : IRabbitMqSender
    {
        public async Task<bool> SendMessageAsync<TMessage>(TMessage model)
        {
            var props = new BasicProperties();
            props.DeliveryMode = DeliveryModes.Persistent;
            return await Send(model, props, MessageTypeEnum.Message);
        }

        public async Task<bool> SendEventAsync<TEvent>(TEvent model)
        {
            var props = new BasicProperties();
            props.DeliveryMode = DeliveryModes.Transient;
            props.Expiration = "0";
            return await Send(model, props, MessageTypeEnum.Event);
        }

        private async Task<bool> Send<TM>(TM model, BasicProperties props, MessageTypeEnum type)
        {
            var (connection, channel) = await rabbitMqConnectionStorage.GetConnectionAsync();

            // Declare an exchange of type 'fanout'
            string exchangeName = exchangeNameGenerator.GetExchangeNameFromType<TM>();

            await queuesGenerator.CreateQueues(channel, exchangeName);

            

            var jsonMessage = JsonSerializer.Serialize(model);
            var body = Encoding.UTF8.GetBytes(jsonMessage);

            await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: string.Empty,
            mandatory: type == MessageTypeEnum.Message,
            body: body,
            basicProperties: props);

            return true;
        }

        private enum MessageTypeEnum
        {
            Message,
            Event
        }
    }
}
