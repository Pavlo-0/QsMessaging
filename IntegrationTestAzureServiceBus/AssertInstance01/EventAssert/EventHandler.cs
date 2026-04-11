using QsMessaging.Public.Handler;
using TestContract.EventContract;

namespace AssertInstance01.MessageAssert
{
    internal class EventHandler : IQsEventHandler<EventContract>
    {
        public Task Consumer(EventContract contractModel)
        {
            if (contractModel.MyEventCount == 0)
            {
                CollectionTestResults.PassTest(TestScenariousEnum.OneEvent);
            }

            return Task.CompletedTask;
        }
    }
}
