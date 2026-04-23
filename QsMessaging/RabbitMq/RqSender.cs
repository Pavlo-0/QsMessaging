using Microsoft.Extensions.Logging;
using QsMessaging.RabbitMq.Interfaces;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.RabbitMq.Services;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Shared;
using QsMessaging.Shared.Interface;
using QsMessaging.Shared.Models.Enums;
using QsMessaging.Shared.Services.Interfaces;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace QsMessaging.RabbitMq
{
    internal class RqSender(
        ILogger<RqSender> logger,
        IRqConnectionService connectionService,
        IRqChannelService channelService,
        IRqExchangeService queuesService,

        IHandlerService handlerService,
        Lazy<ISubscriber> subscriber,

        IRequestResponseMessageStore requestResponseMessageStore) : ISender
    {
        public async Task SendMessageAsync<TMessage>(TMessage model) where TMessage : class
        {
            logger.LogInformation("Sending message");
            logger.LogDebug("{Type}", typeof(TMessage).FullName);

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
        public async Task SendMessageCorrelatedAsync(
            object model,
            string correlationId,
            string replyTo,
            CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Sending message with Correlation ID {CorrelationId} as an internal response to a request.", correlationId);

            var props = new BasicProperties();
            props.DeliveryMode = DeliveryModes.Persistent;
            props.CorrelationId = correlationId;
            await Send(model, model.GetType(), props, MessageTypeEnum.Message, replyTo, cancellationToken);
        }

        public async Task SendEventAsync<TEvent>(TEvent model) where TEvent : class
        {
            logger.LogInformation("Sending event");
            logger.LogDebug("{Type}", typeof(TEvent).FullName);

            var props = new BasicProperties();
            props.DeliveryMode = DeliveryModes.Transient;

            await Send(model, typeof(TEvent), props, MessageTypeEnum.Event);
        }

        public async Task<TResponse> SendRequest<TRequest, TResponse>(TRequest model, CancellationToken cancellationToken = default)
            where TRequest : class
            where TResponse : class
        {
            logger.LogInformation("Sending request");

            var props = new BasicProperties();
            props.DeliveryMode = DeliveryModes.Persistent;
            props.CorrelationId = Guid.NewGuid().ToString();

            logger.LogDebug("{CorrelationId}:{Type}", props.CorrelationId, typeof(TRequest).FullName);

            var responseAsync = requestResponseMessageStore.AddRequestMessageAsync(props.CorrelationId, model, cancellationToken);

            var (handlerRecord, isNew) = handlerService.AddRRResponseHandler<TResponse>();
            if (isNew)
            {
                await subscriber.Value.SubscribeHandlerAsync(handlerRecord, cancellationToken);
            }

            var channelPurpose = HardConfiguration.GetChannelPurpose(handlerRecord.supportedInterfacesType);
            var exchangePurpose = HardConfiguration.GetExchangePurpose(handlerRecord.supportedInterfacesType);
  /*          string exchangeName = await RqChannelPurposeSynchronization.RunExclusiveAsync(
                channelPurpose,
                async () =>
                {
                    var connection = await connectionService.GetOrCreateConnectionAsync(cancellationToken);
                    var channel = await channelService.GetOrCreateChannelAsync(connection, channelPurpose, cancellationToken);
                    return await queuesService.GetOrCreateExchangeAsync(channel, typeof(TResponse), exchangePurpose);
                },
                cancellationToken);
*/
            var connection = await connectionService.GetOrCreateConnectionAsync(cancellationToken);
            var channel = await channelService.GetOrCreateChannelAsync(channelPurpose, cancellationToken);
            var exchangeName = await queuesService.GetOrCreateExchangeAsync(channel, typeof(TResponse), exchangePurpose, cancellationToken);

            props.ReplyTo = exchangeName;

            await Send(model, typeof(TRequest), props, MessageTypeEnum.Message, true, cancellationToken);

            await responseAsync;

            logger.LogDebug("Get response message: {CorrelationId}", props.CorrelationId);

            var respondentMessage = requestResponseMessageStore.GetRespondedMessage<TResponse>(props.CorrelationId);
            requestResponseMessageStore.RemoveMessage(props.CorrelationId);

            return respondentMessage;
        }

        //TODO: join two Send methods into one with optional exchangeName parameter. If exchangeName is null, get or create exchange as before, if not null - use it
        private async Task Send(
            object model,
            Type type,
            BasicProperties props,
            MessageTypeEnum messageType,
            bool isLiveTime = false,
            CancellationToken cancellationToken = default)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model), "You can not send NULL data. You model is null");

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(model));
            var channelPurpose = messageType == MessageTypeEnum.Message ? RqChannelPurpose.MessagePublish : RqChannelPurpose.EventPublish;

            var connection = await connectionService.GetOrCreateConnectionAsync(cancellationToken);
            var channel = await channelService.GetOrCreateChannelAsync(channelPurpose, cancellationToken);
            var exchangeName = await queuesService.GetOrCreateExchangeAsync(
                channel,
                type,
                messageType == MessageTypeEnum.Message && !isLiveTime ? RqExchangePurpose.Permanent : RqExchangePurpose.Temporary,
                cancellationToken);


            await channel.BasicPublishAsync(
                                   exchange: exchangeName,
                                   routingKey: string.Empty,
                                   mandatory: messageType == MessageTypeEnum.Message,
                                   body: body,
                                   basicProperties: props,
                                   cancellationToken: cancellationToken);

            logger.LogInformation("Message has been published");
            logger.LogDebug("{type}", type.FullName);
        }

        private async Task Send(
            object model,
            Type type,
            BasicProperties props,
            MessageTypeEnum messageType,
            string exchangeName,
            CancellationToken cancellationToken = default)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model), "You can not send NULL data. You model is null");

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(model));
            var channelPurpose = messageType == MessageTypeEnum.Message ? RqChannelPurpose.MessagePublish : RqChannelPurpose.EventPublish;

            var connection = await connectionService.GetOrCreateConnectionAsync(cancellationToken);
            var channel = await channelService.GetOrCreateChannelAsync(channelPurpose, cancellationToken);

            await channel.BasicPublishAsync(
                                    exchange: exchangeName,
                                    routingKey: string.Empty,
                                    mandatory: messageType == MessageTypeEnum.Message,
                                    body: body,
                                    basicProperties: props,
                                    cancellationToken: cancellationToken);

            logger.LogInformation("Message has been published");
            logger.LogDebug("{type}", type.FullName);
        }
    }
}
