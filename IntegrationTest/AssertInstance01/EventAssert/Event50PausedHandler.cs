using QsMessaging.Public;
using QsMessaging.Public.Handler;
using System.Collections.Concurrent;
using TestContract.MessageContract;

namespace AssertInstance01.MessageAssert
{
    internal class Event50PausedHandler(IQsMessagingConnectionManager connectionManager) : IQsEventHandler<Event50PausedContract>
    {
        private readonly static ConcurrentBag<Event50PausedContract> _contracts = new ConcurrentBag<Event50PausedContract>();

        public async Task<bool> Consumer(Event50PausedContract contractModel)
        {
            _contracts.Add(contractModel);

            if (contractModel.MyEventCount == 10)
            {
                await connectionManager.Close();
                await Task.Delay(1000);
                await connectionManager.Open();
            }

            if (contractModel.MyEventCount > 45)
            {
                if (_contracts.Count < 45)
                {
                    CollectionTestResults.PassTest(TestScenariousEnum.Event50Paused);
                }
                else
                {
                    CollectionTestResults.FailTest(TestScenariousEnum.Event50Paused);
                }
            }

            return true;
        }
    }
}
