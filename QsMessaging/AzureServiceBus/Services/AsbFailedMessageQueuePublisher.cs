using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using QsMessaging.AzureServiceBus.Services.Interfaces;
using QsMessaging.Public;
using QsMessaging.Public.Handler;
using QsMessaging.Shared;
using QsMessaging.Shared.Models;
using QsMessaging.Shared.Services.Interfaces;
using System.Text.Json;

namespace QsMessaging.AzureServiceBus.Services
{
    internal sealed class AsbFailedMessageQueuePublisher(
        ILogger<AsbFailedMessageQueuePublisher> logger,
        IAsbConnectionService connectionService,
        IAsbQueueService queueService,
        IQsMessagingConfiguration configuration) : IFailedMessageQueuePublisher
    {
        private const int AzureServiceBusEntityMaxLength = 260;
        private const string ErrorQueueSuffix = "-Error";

        public string GetErrorQueueName(ConsumerMessageContext context)
        {
            return FailedMessageQueueName.Create(
                NormalizeEntityName(context.OriginalQueueName),
                ErrorQueueSuffix,
                AzureServiceBusEntityMaxLength);
        }

        public async Task SendAsync(FailedMessageWrapper wrapper, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(wrapper.ErrorQueueName))
            {
                throw new InvalidOperationException("Failed message wrapper does not contain an error queue name.");
            }

            await queueService.GetOrCreateQueueAsync(wrapper.ErrorQueueName, cancellationToken);

            wrapper.SentToErrorQueueUtc = DateTimeOffset.UtcNow;
            var message = new ServiceBusMessage(BinaryData.FromString(JsonSerializer.Serialize(
                wrapper,
                SerializationMetadata.GetJsonSerializerOptions(configuration))))
            {
                ContentType = SerializationMetadata.GetContentType(configuration),
                Subject = typeof(FailedMessageWrapper).FullName,
                CorrelationId = wrapper.CorrelationId,
                MessageId = wrapper.FailedEnvelopeId.ToString("N")
            };

            message.ApplicationProperties[SerializationMetadata.ContractVersionHeader] = SerializationMetadata.GetContractVersion(configuration);
            message.ApplicationProperties[SerializationMetadata.ContractTypeHeader] = typeof(FailedMessageWrapper).FullName;
            message.ApplicationProperties[SerializationMetadata.ContentEncodingHeader] = SerializationMetadata.GetContentEncoding(configuration);
            message.ApplicationProperties["qs-failed-original-queue"] = wrapper.OriginalQueueName;
            message.ApplicationProperties["qs-failed-transport"] = wrapper.TransportName;

            var client = await connectionService.GetOrCreateConnectionAsync(cancellationToken);
            await using var sender = client.CreateSender(wrapper.ErrorQueueName);
            await sender.SendMessageAsync(message, cancellationToken);

            logger.LogInformation(
                "Published failed message {FailedEnvelopeId} to Azure Service Bus error queue {ErrorQueueName}.",
                wrapper.FailedEnvelopeId,
                wrapper.ErrorQueueName);
        }

        private static string NormalizeEntityName(string originalName)
        {
            if (string.IsNullOrWhiteSpace(originalName))
            {
                return "unknown";
            }

            var chars = originalName
                .Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-' ? ch : '_')
                .ToArray();

            var normalized = new string(chars).Trim('_', '.', '-');
            return string.IsNullOrWhiteSpace(normalized)
                ? NameGeneratorBase.HashString(originalName, AzureServiceBusEntityMaxLength - ErrorQueueSuffix.Length)
                : normalized;
        }
    }
}
