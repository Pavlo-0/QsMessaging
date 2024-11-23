using QsMessaging.Public.Handler;
using System.Collections.Concurrent;
using TestContract.MessageContract;

namespace AssertInstance01.MessageAssert
{
    internal class Message100Handler : IQsMessageHandler<Message100Contract>
    {
        private readonly static ConcurrentBag<Message100Contract> _contracts = new ConcurrentBag<Message100Contract>();

        public Task<bool> Consumer(Message100Contract contractModel)
        {
            _contracts.Add(contractModel);

            if (_contracts.Count >= 100)
            {
                var i = 0;
                var isFail = false;
                foreach (var contract in _contracts.OrderBy(v=> v.MyMessageCount))
                {
                    if (contract.MyMessageCount != i)
                    {
                        isFail = true;
                    }
                    i++;
                }

                if (isFail)
                {
                    CollectionTestResults.FailTest(TestScenariousEnum.Messages100);
                }
                else
                {
                    CollectionTestResults.PassTest(TestScenariousEnum.Messages100);
                }
            }

            return Task.FromResult(true);
        }
    }
}
