using QsMessaging.Public.Handler;
using TestContract.MessageContract;

namespace AssertInstance01.MessageAssert
{
    internal class MessageHandler : IQsMessageHandler<MessageContract>
    {
        public Task<bool> Consumer(MessageContract contractModel)
        {
            if (contractModel.MyMessageCount == 0)
            {
                CollectionTestResults.PassTest(TestScenariousEnum.OneMessage);
            }

            return Task.FromResult(true);
        }
    }
}
