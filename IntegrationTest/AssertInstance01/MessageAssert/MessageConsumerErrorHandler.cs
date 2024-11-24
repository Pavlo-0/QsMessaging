using QsMessaging.Public.Handler;
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

    internal class MessageConsumerErrorHandler: IQsMessagingConsumerErrorHandler
    {
        public Task HandleErrorAsync(Exception exception, ErrorConsumerDetail details)
        {
            CollectionTestResults.PassTest(TestScenariousEnum.MessageConsumerError);
            return Task.CompletedTask;
        }
    }
}
