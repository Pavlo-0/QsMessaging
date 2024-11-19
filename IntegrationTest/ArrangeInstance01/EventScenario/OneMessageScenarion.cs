using QsMessaging.Public;
using TestContract.EventContract;

namespace ArrangeInstance01.MessageScenario
{
    internal class OneEventScenarion(IQsMessaging messaging): IScenario
    {
        public bool IsRepeatable => true;

        public async Task Run()
        {
            var message = new EventContract
            {
                MyEventCount = 0
            };

            await messaging.SendMessageAsync(message);
        }
    }
}
