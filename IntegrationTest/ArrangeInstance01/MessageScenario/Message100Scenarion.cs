using QsMessaging.Public;
using TestContract.MessageContract;

namespace ArrangeInstance01.MessageScenario
{
    internal class Message100Scenarion(IQsMessaging messaging): IScenario
    {
        public bool IsRepeatable => false;

        public async Task Run()
        {
            foreach (var i in Enumerable.Range(0, 100))
            {
                var message = new Message100Contract
                {
                    MyMessageCount = i
                };

                await messaging.SendMessageAsync(message);
            }
        }
    }
}
