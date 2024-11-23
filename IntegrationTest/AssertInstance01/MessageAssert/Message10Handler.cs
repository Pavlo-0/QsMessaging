using QsMessaging.Public.Handler;
using System.Collections.Concurrent;
using TestContract.MessageContract;

namespace AssertInstance01.MessageAssert
{
    internal class Message10Handler : IQsMessageHandler<Message10Contract>
    {
        private readonly static ConcurrentBag<Message10Contract> _contracts = new ConcurrentBag<Message10Contract>();

        public Task Consumer(Message10Contract contractModel)
        {
            _contracts.Add(contractModel);

            if (_contracts.Count == 10)
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
                    CollectionTestResults.FailTest(TestScenariousEnum.Messages10);
                }
                else
                {
                    CollectionTestResults.PassTest(TestScenariousEnum.Messages10);
                }
            }

            return Task.CompletedTask;
        }
    }
}
