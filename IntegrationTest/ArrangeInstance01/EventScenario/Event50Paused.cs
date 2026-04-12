using QsMessaging.Public;
using TestContract.MessageContract;

namespace ArrangeInstance01.MessageScenario
{
    internal class Event50Paused(IQsMessaging messaging) : IScenario
    {
        public bool IsRepeatable => false;

        public async Task Run()
        {
            await Task.Delay(1000);

            foreach (var i in Enumerable.Range(0, 50))
            {
                var message = new Event50PausedContract
                {
                    MyEventCount = i
                };

                await messaging.SendEventAsync(message);

                await Task.Delay(200);
            }
        }
    }
}
