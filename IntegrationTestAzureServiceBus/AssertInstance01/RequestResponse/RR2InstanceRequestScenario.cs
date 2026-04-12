using AssertInstance01;
using QsMessaging.Public;
using TestContract.RequestResponse;

namespace ArrangeInstance01.RequestResponse
{
    internal class RR2InstanceRequestScenario(IQsMessaging messaging) : IScenario
    {
        public bool IsRepeatable => throw new NotImplementedException();

        public async Task Run()
        {
            await Task.Delay(1500);

            var Ids = new string[] { "2", "4", "6", "8" };
            var IdsResult = new string[] { "2", "4", "6", "8" };
            var isPassTest = true;

            for (int i = 0; i < Ids.Length; i++)
            {
                var result = await messaging.RequestResponse<RRRequest2InstanceRequestContract, RRresponse2InstanceRequestContract>(new RRRequest2InstanceRequestContract(Ids[i], "Some message"));
                if (result.Id != IdsResult[i])
                {
                    isPassTest = false;
                }
                await Task.Delay(100);
            }

            if (isPassTest)
                CollectionTestResults.PassTest(TestScenariousEnum.TwoInstanceRequest);
            else
                CollectionTestResults.FailTest(TestScenariousEnum.TwoInstanceRequest);
        }
    }
}