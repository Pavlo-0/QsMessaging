using QsMessaging.Public;
using TestContract.RequestResponse;

namespace AssertInstance01.RequestResponse
{
    internal class RRRequest(IQsMessaging qsMessaging, IScenarioExecutionGate scenarioExecutionGate): IScenario
    {
        public async Task Run()
        {
            await Task.Delay(1000);
            await scenarioExecutionGate.WaitUntilReadyAsync();

            var answer = await qsMessaging.RequestResponse<RRRequestAddContract, RRResponseAddContract>(new RRRequestAddContract() {
                Number1 = 1,
                Number2 = 2
            });

            if (answer.SumAnswer == 3)
            {
                CollectionTestResults.PassTest(TestScenariousEnum.RequestResponse1);
            }
            else
            {
                CollectionTestResults.FailTest(TestScenariousEnum.RequestResponse1);
            }
        }
    }
}
