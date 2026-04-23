using QsMessaging.Public;
using TestContract.EventContract;

namespace ArrangeInstance01.MessageScenario
{
    internal class Event50Scenarion(IQsMessaging messaging) : IScenario
    {
        public bool IsRepeatable => false;

        public async Task Run()
        {
            foreach (var i in Enumerable.Range(0, 50))
            {
                var message = new Event50Contract
                {
                    MyEventCount = i
                };

                await messaging.SendEventAsync(message);
            }
        }
    }
}
