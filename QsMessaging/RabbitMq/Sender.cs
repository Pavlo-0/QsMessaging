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
        //ISubscriber subscriber,
        Lazy<ISubscriber> subscriber,
        /*IServiceProvider serviceProvider,
        IQueueService queueService,
        
        IConsumerService consumerService,*/
        IRequestResponseMessageStore requestResponseMessageStore) : IRabbitMqSender, ISender
    {
        public async Task SendMessageAsync<TMessage>(TMessage model) where TMessage : class
        {
            var props = new BasicProperties();
            props.DeliveryMode = DeliveryModes.Persistent;
            await Send(model, typeof(TMessage), props, MessageTypeEnum.Message);
        }

        public async Task SendMessageCorrelationAsync(object model, string? correlationId)
        {
            var props = new BasicProperties();
            props.DeliveryMode = DeliveryModes.Persistent;
            if (!string.IsNullOrEmpty(correlationId))
            {
                props.CorrelationId = correlationId;
            }
            await Send(model, model.GetType(), props, MessageTypeEnum.Message);
        }

        public async Task SendEventAsync<TEvent>(TEvent model) where TEvent : class
        {
            var props = new BasicProperties();
            props.DeliveryMode = DeliveryModes.Transient;
            props.Expiration = "0";

            await Send(model, typeof(TEvent), props, MessageTypeEnum.Event);
        }

        public async Task<TResponse> SendRequest<TRequest, TResponse>(TRequest model) where TRequest : class where TResponse : class 
        {
            var props = new BasicProperties();
            props.DeliveryMode = DeliveryModes.Persistent;
            props.CorrelationId = Guid.NewGuid().ToString();

            requestResponseMessageStore.AddRequestMessage(props.CorrelationId, model);

            //Before Send message we should do listening channel for response
            /*
            var rrrHandlerType = typeof(IRequestResponseResponseHandler);

            await subscriber.SubscribeHandlerAsync(handlerService.GetHandlers(rrrHandlerType).First());

            var connection = await connectionService.GetOrCreateConnectionAsync();
            var channel = await channelService.GetOrCreateChannelAsync(connection, HardConfiguration.GetChannelPurposeByInterfaceTypes(rrrHandlerType));
            string exchangeName = await queuesService.GetOrCreateExchangeAsync(channel, typeof(TResponse));
            var queueName = await queueService.GetOrCreateQueuesAsync(
                channel,
                rrrHandlerType, 
                exchangeName, 
                HardConfiguration.GetQueueByInterfaceTypes(rrrHandlerType));

            await consumerService.GetOrCreateConsumerAsync(
                channel, 
                queueName,
                serviceProvider,
                handlerService.GetHandlers(rrrHandlerType).First());*/

            var handlerRecord = handlerService.AddRRResponseHandler<TResponse>();
            await subscriber.Value.SubscribeHandlerAsync(handlerRecord);

            await Send(model, typeof(TRequest),  props, MessageTypeEnum.Message);

            while (!requestResponseMessageStore.IsRespondedMessage(props.CorrelationId))
            {
                await Task.Delay(100);
            } 

            (object message, Type messageType) = requestResponseMessageStore.GetRespondedMessage(props.CorrelationId);
            if (message as TResponse == null || messageType == null)
                throw new ArgumentNullException(nameof(TRequest), "Respond model is null");

            return message as TResponse;
        }

        private async Task Send(object model, Type type, BasicProperties props, MessageTypeEnum messageType)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model), "You can not send NULL data. You model is null");

            var connection = await connectionService.GetOrCreateConnectionAsync();
            var channel = await channelService.GetOrCreateChannelAsync(connection,
                messageType == MessageTypeEnum.Message ? ChannelPurpose.MessagePublish : ChannelPurpose.EventPublish);

            string exchangeName = await queuesService.GetOrCreateExchangeAsync(channel, type);

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
