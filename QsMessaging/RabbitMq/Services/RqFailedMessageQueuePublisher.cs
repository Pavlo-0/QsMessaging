using Microsoft.Extensions.Logging;
using QsMessaging.Public;
using QsMessaging.Public.Handler;
using QsMessaging.RabbitMq.Models.Enums;
using QsMessaging.RabbitMq.Services.Interfaces;
using QsMessaging.Shared;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services.Interfaces;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace QsMessaging.RabbitMq.Services
{
    internal sealed class RqFailedMessageQueuePublisher(
        ILogger<RqFailedMessageQueuePublisher> logger,
        IRqChannelService channelService,
        IQsMessagingConfiguration configuration) : IFailedMessageQueuePublisher
    {
        private const int RabbitMqShortStringMaxLength = 255;
        private const string ErrorQueueSuffix = ":Error";

        public string GetErrorQueueName(ConsumerMessageContext context)
        {
            return FailedMessageQueueName.Create(context.OriginalQueueName, ErrorQueueSuffix, RabbitMqShortStringMaxLength);
        }

        public async Task SendAsync(FailedMessageWrapper wrapper, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(wrapper.ErrorQueueName))
            {
                throw new InvalidOperationException("Failed message wrapper does not contain an error queue name.");
            }

            var channel = await channelService.GetOrCreateChannelAsync(RqChannelPurpose.MessagePublish, cancellationToken);

            await RqChannelExecutor.ExecuteAsync(
                channel,
                async token =>
                {
                    await channel.ExchangeDeclareAsync(
                        exchange: wrapper.ErrorQueueName,
                        type: ExchangeType.Fanout,
                        durable: true,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: token);

                    await channel.QueueDeclareAsync(
                        queue: wrapper.ErrorQueueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null,
                        cancellationToken: token);

                    await channel.QueueBindAsync(
                        queue: wrapper.ErrorQueueName,
                        exchange: wrapper.ErrorQueueName,
                        routingKey: string.Empty,
                        arguments: null,
                        cancellationToken: token);

                    wrapper.SentToErrorQueueUtc = DateTimeOffset.UtcNow;
                    var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                        wrapper,
                        SerializationMetadata.GetJsonSerializerOptions(configuration)));

                    var props = new BasicProperties
                    {
                        DeliveryMode = DeliveryModes.Persistent,
                        ContentType = SerializationMetadata.GetContentType(configuration),
                        ContentEncoding = SerializationMetadata.GetContentEncoding(configuration),
                        Type = typeof(FailedMessageWrapper).FullName,
                        MessageId = wrapper.FailedEnvelopeId.ToString("N"),
                        CorrelationId = wrapper.CorrelationId
                    };
                    props.Headers ??= new Dictionary<string, object?>();
                    props.Headers[SerializationMetadata.ContractVersionHeader] = SerializationMetadata.GetContractVersion(configuration);
                    props.Headers[SerializationMetadata.ContractTypeHeader] = typeof(FailedMessageWrapper).FullName;

                    await channel.BasicPublishAsync(
                        exchange: wrapper.ErrorQueueName,
                        routingKey: string.Empty,
                        mandatory: false,
                        body: body,
                        basicProperties: props,
                        cancellationToken: token);
                },
                cancellationToken);

            logger.LogInformation(
                "Published failed message {FailedEnvelopeId} to RabbitMQ error queue {ErrorQueueName}.",
                wrapper.FailedEnvelopeId,
                wrapper.ErrorQueueName);
        }
    }
}
