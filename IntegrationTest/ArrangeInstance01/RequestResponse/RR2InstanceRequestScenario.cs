using QsMessaging.Public;
using TestContract.RequestResponse;

namespace ArrangeInstance01.RequestResponse
{
    internal class RR2InstanceRequestScenario(IQsMessaging messaging) : IScenario
    {
        public bool IsRepeatable => false;

        public async Task Run()
        {
            await Task.Delay(1500);
            var Ids = new string[] { "1", "3", "4", "6" };

            for (int i = 0; i < Ids.Length; i++) {
                await messaging.RequestResponse<RRRequest2InstanceRequestContract, RRresponse2InstanceRequestContract>(
                    new RRRequest2InstanceRequestContract(Ids[i], "Some message"));
                await Task.Delay(100);
            }
        }
    }
}
