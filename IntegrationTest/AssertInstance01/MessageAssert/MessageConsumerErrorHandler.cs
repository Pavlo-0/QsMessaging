using QsMessaging.Public;
using QsMessaging.Public.Handler;
using RabbitMQ.Client;
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
        //TODO: add support Azure service bus for integration test
        private const string OriginalQueueName = "Qs:TestContract.MessageContract.MessageConsumerErrorContract:permanent";
        private const string ErrorQueueName = "Qs:TestContract.MessageContract.MessageConsumerErrorContract:permanent:Error";

        public async Task HandleErrorAsync(Exception exception, ErrorConsumerDetail details)
        {
            if (details.GenericTypeName != typeof(MessageConsumerErrorContract).FullName)
            {
                return;
            }

            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = configuration.RabbitMQ.Host,
                    UserName = configuration.RabbitMQ.UserName,
                    Password = configuration.RabbitMQ.Password,
                    Port = configuration.RabbitMQ.Port,
                    VirtualHost = configuration.RabbitMQ.VirtualHost
                };

                await using var connection = await factory.CreateConnectionAsync("MessageConsumerErrorHandler");
                await using var channel = await connection.CreateChannelAsync();
                await channel.QueueDeclarePassiveAsync(ErrorQueueName);

                while (true)
                {
                    var result = await channel.BasicGetAsync(ErrorQueueName, autoAck: true);
                    if (result is null)
                    {
                        break;
                    }

                    var wrapper = JsonSerializer.Deserialize<FailedMessageWrapper>(
                        Encoding.UTF8.GetString(result.Body.ToArray()));

                    if (wrapper is not null && IsExpectedWrapper(wrapper, details.FailedMessage))
                    {
                        CollectionTestResults.PassTest(TestScenariousEnum.MessageConsumerError);
                        return;
                    }
                }

                CollectionTestResults.FailTest(TestScenariousEnum.MessageConsumerError);
            }
            catch
            {
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
                && wrapper.TransportName == "RabbitMQ"
                && wrapper.OriginalQueueName == OriginalQueueName
                && wrapper.ErrorQueueName == ErrorQueueName
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
    }
}
