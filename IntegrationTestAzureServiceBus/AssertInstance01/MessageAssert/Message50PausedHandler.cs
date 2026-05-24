using QsMessaging.Public;
using QsMessaging.Public.Handler;
using System.Collections.Concurrent;
using TestContract.MessageContract;

namespace AssertInstance01.MessageAssert
{
    internal class Message50PausedHandler(IQsMessagingConnectionManager connectionManager) : IQsMessageHandler<Message50PausedContract>
    {
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<int, byte>> _contractsByRunId = new();
        private static readonly ConcurrentDictionary<string, byte> _pauseTriggeredRunIds = new();

        public async Task Consumer(Message50PausedContract contractModel)
        {
            if (string.IsNullOrWhiteSpace(contractModel.RunId))
            {
                return;
            }

            var contracts = _contractsByRunId.GetOrAdd(
                contractModel.RunId,
                _ => new ConcurrentDictionary<int, byte>());
            contracts.TryAdd(contractModel.MyMessageCount, 0);

            if (contractModel.MyMessageCount == 30 &&
                _pauseTriggeredRunIds.TryAdd(contractModel.RunId, 0))
            {
                await connectionManager.Close();
                await Task.Delay(1000);
                await connectionManager.Open();
            }
            

            if (contracts.Count == 50)
            {
                var isFail = !Enumerable.Range(0, 50).All(contracts.ContainsKey);

                if (isFail)
                {
                    CollectionTestResults.FailTest(TestScenariousEnum.Message50Paused);
                }
                else
                {
                    CollectionTestResults.PassTest(TestScenariousEnum.Message50Paused);
                }
            }
        }
    }
}
