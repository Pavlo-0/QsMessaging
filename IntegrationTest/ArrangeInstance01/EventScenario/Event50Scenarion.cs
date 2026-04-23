using QsMessaging.Public;
using TestContract.EventContract;

namespace ArrangeInstance01.MessageScenario
{
    internal class Event50Scenarion(IQsMessaging messaging) : IScenario
    {
        public bool IsRepeatable => false;

        public async Task Run()
        {
            var tasks = Enumerable.Range(0, 50)
                .Select(i =>
                {
                    var message = new Event50Contract
                    {
                        MyEventCount = i
                    };

                    return messaging.SendEventAsync(message);
                });

            await Task.WhenAll(tasks);
        }
    }
}
