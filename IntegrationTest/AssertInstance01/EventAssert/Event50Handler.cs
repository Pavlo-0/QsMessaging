using QsMessaging.Public.Handler;
using System.Collections.Concurrent;
using TestContract.EventContract;

namespace AssertInstance01.MessageAssert
{
    internal class Event50Handler : IQsEventHandler<Event50Contract>
    {
        private static readonly ConcurrentBag<Event50Contract> _contracts = new();

        public Task Consumer(Event50Contract contractModel)
        {
            _contracts.Add(contractModel);

            if (_contracts.Count >= 50)
            {
                var i = 0;
                var isFail = false;
                foreach (var contract in _contracts.OrderBy(v => v.MyEventCount))
                {
                    if (contract.MyEventCount != i)
                    {
                        isFail = true;
                    }

                    i++;
                }

                if (isFail)
                {
                    CollectionTestResults.FailTest(TestScenariousEnum.Event50);
                }
                else
                {
                    CollectionTestResults.PassTest(TestScenariousEnum.Event50);
                }
            }

            return Task.CompletedTask;
        }
    }
}
