using QsMessaging.Public.Handler;
using TestContract.EventContract;

namespace AssertInstance01.MessageAssert
{
    internal class EventHandler : IQsEventHandler<EventContract>
    {
        public Task<bool> Consumer(EventContract contractModel)
        {
            if (contractModel.MyEventCount == 0)
            {
                CollectionTestResults.PassTest(TestScenariousEnum.OneEvent);
            }

            return Task.FromResult(true);
        }
    }
}
