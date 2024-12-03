using QsMessaging.Public;
using TestContract.RequestResponse;

namespace AssertInstance01.RequestResponse
{
    public class RRRequest10AtOne(IQsMessaging qsMessaging) : IScenario
    {
        public async Task Run()
        {
            await Task.Delay(3000);

            var isFailed = false;
            var requests = new List<Task<RRResponseAddContract>>();
            for (int i = 0; i < 10; i++)
            {

                requests.Add(qsMessaging.RequestResponse<RRRequestAddContract, RRResponseAddContract>(new RRRequestAddContract()
                {
                    Number1 = 100 + i,
                    Number2 = 4
                }));
            }

            Task.WaitAll(requests.ToArray());

            for (int i = 0; i < 10; i++)
            {
                var answer = await requests[i];
                if (answer.SumAnswer != (104 + i))
                {
                    isFailed = true;
                }
            }

            if (!isFailed)
            {
                CollectionTestResults.PassTest(TestScenariousEnum.RequestResponse10AtOnce);
            }
            else
            {
                CollectionTestResults.FailTest(TestScenariousEnum.RequestResponse10AtOnce);
            }
        }
    }
}
