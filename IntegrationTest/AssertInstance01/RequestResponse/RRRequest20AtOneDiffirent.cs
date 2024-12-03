using QsMessaging.Public;
using TestContract.RequestResponse;

namespace AssertInstance01.RequestResponse
{
    public class RRRequest20AtOneDiffirent(IQsMessaging qsMessaging) : IScenario
    {
        public async Task Run()
        {
            await Task.Delay(4000);

            var isFailed = false;
            var addRequests = new List<Task<RRResponseAddContract>>();
            var subtractionRequests = new List<Task<RRResponseSubtractionContract>>();
            for (int i = 0; i < 10; i++)
            {

                addRequests.Add(qsMessaging.RequestResponse<RRRequestAddContract, RRResponseAddContract>(new RRRequestAddContract()
                {
                    Number1 = 1000 + i,
                    Number2 = 5
                }));

                subtractionRequests.Add(qsMessaging.RequestResponse<RRRequestSubtractionContract, RRResponseSubtractionContract>(new RRRequestSubtractionContract()
                {
                    Number1 = 100 - i,
                    Number2 = 1
                }));
            }

            Task.WaitAll(addRequests.ToArray());
            Task.WaitAll(subtractionRequests.ToArray());

            for (int i = 0; i < 10; i++)
            {
                var addAnswer = await addRequests[i];
                if (addAnswer.SumAnswer != (1005 + i))
                {
                    isFailed = true;
                }

                var subtractionAnswer = await subtractionRequests[i];
                if (subtractionAnswer.SubtractionAnswer != (99 - i))
                {
                    isFailed = true;
                }
            }

            if (!isFailed)
            {
                CollectionTestResults.PassTest(TestScenariousEnum.RequestResponse20AtOnceDif);
            }
            else
            {
                CollectionTestResults.FailTest(TestScenariousEnum.RequestResponse20AtOnceDif);
            }
        }
    }
}
