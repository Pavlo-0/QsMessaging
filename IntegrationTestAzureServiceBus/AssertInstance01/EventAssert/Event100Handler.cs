using QsMessaging.Public.Handler;
using TestContract.EventContract;

namespace AssertInstance01.MessageAssert
{
    internal class Event100Handler : IQsEventHandler<Event100Contract>
    {
        private static int _eventCount = 0;
        public Task Consumer(Event100Contract contractModel)
        {
            Interlocked.Increment(ref _eventCount);


            if (_eventCount > 190)
            {
                CollectionTestResults.PassTest(TestScenariousEnum.Event100);
            }

            return Task.CompletedTask;
        }
    }
}
