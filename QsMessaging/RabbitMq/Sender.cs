using QsMessaging.Public;
using QsMessaging.RabbitMq.Interface;
using QsMessaging.RabbitMq.Interfaces;
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
        IExchangeService queuesService,

        IHandlerService handlerService,
        Lazy<ISubscriber> subscriber,

        IRequestResponseMessageStore requestResponseMessageStore) : IRabbitMqSender, ISender
    {
        public async Task SendMessageAsync<TMessage>(TMessage model) where TMessage : class
        {
            var props = new BasicProperties();
            props.DeliveryMode = DeliveryModes.Persistent;
            await Send(model, typeof(TMessage), props, MessageTypeEnum.Message);
        }

        /// <summary>
        /// ISender interface. Used for internal purpose. 
        /// Answer for RequestResponse strategy
        /// </summary>
        /// <param name="model"></param>
        /// <param name="correlationId"></param>
        /// <returns></returns>
        public async Task SendMessageCorrelationAsync(object model, string correlationId)
        {
            var props = new BasicProperties();
            props.DeliveryMode = DeliveryModes.Persistent;
            props.CorrelationId = correlationId;
            await Send(model, model.GetType(), props, MessageTypeEnum.Message, true);
        }

        public async Task SendEventAsync<TEvent>(TEvent model) where TEvent : class
        {
            var props = new BasicProperties();
            props.DeliveryMode = DeliveryModes.Transient;
            props.Expiration = "0";

            await Send(model, typeof(TEvent), props, MessageTypeEnum.Event);
        }

        public async Task<TResponse> SendRequest<TRequest, TResponse>(TRequest model, CancellationToken cancellationToken = default) where TRequest : class where TResponse : class 
        {
            var props = new BasicProperties();
            props.DeliveryMode = DeliveryModes.Persistent;
            props.CorrelationId = Guid.NewGuid().ToString();

            var responseAsync = requestResponseMessageStore.AddRequestMessageAsync(props.CorrelationId, model, cancellationToken);

            var handlerRecord = handlerService.AddRRResponseHandler<TResponse>();
            await subscriber.Value.SubscribeHandlerAsync(handlerRecord);

            await Send(model, typeof(TRequest),  props, MessageTypeEnum.Message, true);

            await responseAsync;

            return requestResponseMessageStore.GetRespondedMessage<TResponse>(props.CorrelationId);
        }

        private async Task Send(object model, Type type, BasicProperties props, MessageTypeEnum messageType, bool isLiveTime = false)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model), "You can not send NULL data. You model is null");

            var connection = await connectionService.GetOrCreateConnectionAsync();
            var channel = await channelService.GetOrCreateChannelAsync(connection,
                messageType == MessageTypeEnum.Message ? ChannelPurpose.MessagePublish : ChannelPurpose.EventPublish);

            string exchangeName = await queuesService.GetOrCreateExchangeAsync(channel, type,
                messageType == MessageTypeEnum.Message && !isLiveTime ? ExchangePurpose.Permanent : ExchangePurpose.Temporary);

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(model));

            await channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: string.Empty,
            mandatory: messageType == MessageTypeEnum.Message,
            body: body,
            basicProperties: props);
        }

        private enum MessageTypeEnum
        {
            Message,
            Event
        }
    }
}
