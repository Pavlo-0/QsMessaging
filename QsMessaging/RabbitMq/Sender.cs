﻿using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace QsMessaging.RabbitMq
{
    internal class Sender(
        IConnectionService connectionService,
        IChannelService channelService,
        IExchangeService queuesService) : IRabbitMqSender
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
            if (model == null)
                throw new ArgumentNullException(nameof(model), "You can not send NULL data. You model is null");

            var connection = await connectionService.GetOrCreateConnectionAsync();
            var channel = await channelService.GetOrCreateChannelAsync(connection,
                type == MessageTypeEnum.Message ? ChannelService.ChannelPurpose.MessagePublish : ChannelService.ChannelPurpose.EventPublish
                );

            string exchangeName = await queuesService.CreateExchange(channel, model.GetType());

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
