using QsMessaging.Public.Handler;
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
        IServiceProvider serviceProvider) : IRabbitMqSender
    {
        public async Task<bool> SendMessageAsync<TMessage>(TMessage model) where TMessage : class
        {
            var props = new BasicProperties();
            props.DeliveryMode = DeliveryModes.Persistent;
            return await Send(model, props, MessageTypeEnum.Message);
        }

        public async Task<bool> SendEventAsync<TEvent>(TEvent model) where TEvent : class
        {
            var props = new BasicProperties();
            props.DeliveryMode = DeliveryModes.Transient;
            props.Expiration = "0";
            return await Send(model, props, MessageTypeEnum.Event);
        }

        private async Task<bool> Send<TM>(TM model, BasicProperties props, MessageTypeEnum type) where TM : class
        {
            try
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
                try
                {
                    await channel.BasicPublishAsync(
                    exchange: exchangeName,
                    routingKey: string.Empty,
                    mandatory: type == MessageTypeEnum.Message,
                    body: body,
                    basicProperties: props);
                }
                catch (Exception ex)
                {
                    Error(handlerService, serviceProvider, model, ex, type, ErrorPublishType.PublishProblem);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Error(handlerService, serviceProvider, model, ex, type, ErrorPublishType.EstablishPublishConnection);
                return false;
            }
        }

        private static void Error<TM>(
            IHandlerService handlerService,
            IServiceProvider serviceProvider,
            TM model,
            Exception ex,
            MessageTypeEnum messageType,
            ErrorPublishType errorType) where TM : class
        {
            try
            {
                var listErrorHandlers = handlerService.GetPublishErrorHandlers();
                listErrorHandlers.Where(handler => handler.GenericType == model.GetType()).ToList().ForEach(record =>
                {
                    var errorInstance = serviceProvider.GetService(record.ConcreteHandlerInterfaceType);
                    var errorMethod = record.HandlerType.GetMethod(nameof(IQsMessagingPublishErrorHandler<object>.HandlerErrorAsync));

                    var detailModel = new ErrorPublishDetail<TM>(model, errorType, messageType.ToString());

                    if (errorMethod != null)
                    {
                        errorMethod.Invoke(errorInstance, new[] { ex, detailModel as object });
                    }
                });
            }
            catch
            {
                //TODO: Log error
            }
        }

        private enum MessageTypeEnum
        {
            Message,
            Event
        }
    }
}
