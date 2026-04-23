using QsMessaging.Public;
using TestContract.RequestResponse;

namespace AssertInstance01.RequestResponse
{
    internal class RRRequest10(IQsMessaging qsMessaging, IScenarioExecutionGate scenarioExecutionGate): IScenario
    {
        public async Task Run()
        {
            await Task.Delay(2000);
            var isFailed = false;
            for (int i = 0; i < 10; i++)
            {
                await scenarioExecutionGate.WaitUntilReadyAsync();
                var answer = await qsMessaging.RequestResponse<RRRequestAddContract, RRResponseAddContract>(new RRRequestAddContract()
                {
                    Number1 = 10 + i,
                    Number2 = 2
                });

                if (answer.SumAnswer != (12 + i) )
                {
                    isFailed = true;
                }
             }

            if (!isFailed)
            {
                CollectionTestResults.PassTest(TestScenariousEnum.RequestResponse10OneByOne);
            }
            else
            {
                CollectionTestResults.FailTest(TestScenariousEnum.RequestResponse10OneByOne);
            }
        }
    }
}
