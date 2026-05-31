using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using QsMessaging.AzureServiceBus;
using QsMessaging.Public;
using QsMessaging.Public.Handler;
using System.Text;
using System.Text.Json;
using TestContract.MessageContract;

namespace AssertInstance01.MessageAssert
{
    internal class MessageConsumerError : IQsMessageHandler<MessageConsumerErrorContract>
    {
        public Task Consumer(MessageConsumerErrorContract contractModel)
        {
            throw new MySpecialSetToUnhandledException("MessageConsumerErrorContract");
        }
    }

    public class MySpecialSetToUnhandledException : Exception
    {
        public MySpecialSetToUnhandledException(string? message) : base(message)
        {
        }
    }

    internal class MessageConsumerErrorHandler(IQsMessagingConfiguration configuration) : IQsMessagingConsumerErrorHandler
    {
        private const string OriginalQueueName = "Qs_P_ddd2ffb979a16b6ac42e6cc5d3b1e52d36cca7acbf";
        private const string ErrorQueueName = "Qs_P_ddd2ffb979a16b6ac42e6cc5d3b1e52d36cca7acbf-Error";
        private const string EmulatorErrorQueueName = "qs_p_ddd2ffb979a16b6ac42e6cc5d3b1e52d36cca7acbf-error";

        public async Task HandleErrorAsync(Exception exception, ErrorConsumerDetail details)
        {
            if (details.GenericTypeName != typeof(MessageConsumerErrorContract).FullName)
            {
                return;
            }

            try
            {
                var administrationClient = new ServiceBusAdministrationClient(
                    GetAdministrationConnectionString(configuration.AzureServiceBus));
                if (!(await administrationClient.QueueExistsAsync(EmulatorErrorQueueName)).Value)
                {
                    CollectionTestResults.FailTest(TestScenariousEnum.MessageConsumerError);
                    return;
                }

                await using var client = new ServiceBusClient(GetClientConnectionString(configuration.AzureServiceBus));
                await using var receiver = client.CreateReceiver(
                    EmulatorErrorQueueName,
                    new ServiceBusReceiverOptions
                    {
                        ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
                    });

                var isFirstReceive = true;
                while (true)
                {
                    var message = await receiver.ReceiveMessageAsync(
                        isFirstReceive
                            ? TimeSpan.FromSeconds(5)
                            : TimeSpan.FromMilliseconds(200));
                    isFirstReceive = false;

                    if (message is null)
                    {
                        break;
                    }

                    var wrapper = JsonSerializer.Deserialize<FailedMessageWrapper>(message.Body.ToString());
                    if (wrapper is not null && IsExpectedWrapper(wrapper, details.FailedMessage))
                    {
                        CollectionTestResults.PassTest(TestScenariousEnum.MessageConsumerError);
                        return;
                    }

                }

                Console.WriteLine("MessageConsumerError failed: expected Azure Service Bus failed-message wrapper was not found.");
                CollectionTestResults.FailTest(TestScenariousEnum.MessageConsumerError);
            }
            catch (Exception handlerException)
            {
                Console.WriteLine($"MessageConsumerError failed: {handlerException}");
                CollectionTestResults.FailTest(TestScenariousEnum.MessageConsumerError);
            }
        }

        private static bool IsExpectedWrapper(
            FailedMessageWrapper wrapper,
            FailedMessageWrapper? errorHandlerWrapper)
        {
            var payload = wrapper.OriginalMessageBodyText is null
                ? null
                : JsonSerializer.Deserialize<MessageConsumerErrorContract>(wrapper.OriginalMessageBodyText);

            return errorHandlerWrapper is not null
                && wrapper.FailedEnvelopeId == errorHandlerWrapper.FailedEnvelopeId
                && wrapper.TransportName == "AzureServiceBus"
                && string.Equals(wrapper.OriginalQueueName, OriginalQueueName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(wrapper.ErrorQueueName, ErrorQueueName, StringComparison.OrdinalIgnoreCase)
                && payload?.MyMessageCount == 0
                && wrapper.HandlerType == typeof(MessageConsumerError).FullName
                && wrapper.HandlerAttempts == 2
                && wrapper.ConfiguredMaxRetryAttempts == 1
                && wrapper.Errors.Any(error =>
                    error.ExceptionType == typeof(MySpecialSetToUnhandledException).FullName
                    && error.Message == "MessageConsumerErrorContract")
                && wrapper.CreatedUtc.Offset == TimeSpan.Zero
                && wrapper.SentToErrorQueueUtc?.Offset == TimeSpan.Zero
                && wrapper.OriginalMessageHeaders.Count > 0;
        }

        private static string GetClientConnectionString(QsAzureServiceBusConfiguration configuration)
        {
            return ApplyEmulatorPortIfNeeded(
                configuration.ConnectionString,
                configuration.EmulatorAmqpPort,
                overwriteExplicitPort: false);
        }

        private static string GetAdministrationConnectionString(QsAzureServiceBusConfiguration configuration)
        {
            if (!string.IsNullOrWhiteSpace(configuration.AdministrationConnectionString))
            {
                return ApplyEmulatorPortIfNeeded(
                    configuration.AdministrationConnectionString,
                    configuration.EmulatorManagementPort,
                    overwriteExplicitPort: false);
            }

            return ApplyEmulatorPortIfNeeded(
                configuration.ConnectionString,
                configuration.EmulatorManagementPort,
                overwriteExplicitPort: true);
        }

        private static string ApplyEmulatorPortIfNeeded(
            string connectionString,
            int emulatorPort,
            bool overwriteExplicitPort)
        {
            var sections = connectionString
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(segment => segment.Split('=', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

            if (!sections.TryGetValue("UseDevelopmentEmulator", out var useDevelopmentEmulator)
                || !bool.TryParse(useDevelopmentEmulator, out var isDevelopmentEmulator)
                || !isDevelopmentEmulator
                || !sections.TryGetValue("Endpoint", out var endpointRaw)
                || !Uri.TryCreate(endpointRaw, UriKind.Absolute, out var endpoint)
                || (!overwriteExplicitPort && !endpoint.IsDefaultPort))
            {
                return connectionString;
            }

            sections["Endpoint"] = new UriBuilder(endpoint)
            {
                Port = emulatorPort
            }.Uri.GetLeftPart(UriPartial.Authority);

            var builder = new StringBuilder();
            foreach (var section in sections)
            {
                builder.Append(section.Key)
                    .Append('=')
                    .Append(section.Value)
                    .Append(';');
            }

            return builder.ToString();
        }
    }
}
