using QsMessaging.Public;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestContract.RequestResponse;

namespace AssertInstance01.RequestResponse
{
    public class RRRequest(IQsMessaging qsMessaging): IScenario
    {
        public async Task Run()
        {
            await Task.Delay(1000 * 5);

            var answer = await qsMessaging.RequestResponse<RRRequestContract, RRResponseContract>(new RRRequestContract() {
                Number1 = 1,
                Number2 = 2
            });

            if (answer.SumAnswer == 2)
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
