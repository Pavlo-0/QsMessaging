using QsMessaging.Public;
using QsMessaging.Public.Handler;
using System.Collections.Concurrent;
using TestContract.MessageContract;

namespace AssertInstance01.MessageAssert
{
    internal class Event50PausedHandler(
        IQsMessagingConnectionManager connectionManager,
        IScenarioExecutionGate scenarioExecutionGate) : IQsEventHandler<Event50PausedContract>
    {
        private readonly static ConcurrentBag<Event50PausedContract> _contracts = new ConcurrentBag<Event50PausedContract>();

        public async Task Consumer(Event50PausedContract contractModel)
        {
            _contracts.Add(contractModel);

            if (contractModel.MyEventCount == 10)
            {
                var scenarioExecutionBlock = scenarioExecutionGate.BeginBlock();

                try
                {
                    await connectionManager.Close();
                }
                catch
                {
                    await scenarioExecutionBlock.DisposeAsync();
                    throw;
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        await connectionManager.Open();
                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        await scenarioExecutionBlock.DisposeAsync();
                    }
                });
            }

            if (contractModel.MyEventCount > 45)
            {
                if (_contracts.Count <= 45)
                {
                    CollectionTestResults.PassTest(TestScenariousEnum.Event50Paused);
                }
                else
                {
                    CollectionTestResults.FailTest(TestScenariousEnum.Event50Paused);
                }
            }

        }
    }
}
