using QsMessaging.Public;
using TestContract.EventContract;

namespace ArrangeInstance01.MessageScenario
{
    internal class Event100Scenarion(IQsMessaging messaging): IScenario
    {
        public bool IsRepeatable => true;

        public async Task Run()
        {
            foreach (var i in Enumerable.Range(0, 100))
            {
                var message = new Event100Contract
                {
                    MyEventCount = i
                };

                await messaging.SendEventAsync(message);
            }
        }
    }
}
