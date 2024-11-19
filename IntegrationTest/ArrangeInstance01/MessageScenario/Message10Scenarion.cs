using QsMessaging.Public;
using TestContract.MessageContract;

namespace ArrangeInstance01.MessageScenario
{
    internal class Message10Scenarion(IQsMessaging messaging): IScenario
    {
        public bool IsRepeatable => false;

        public async Task Run()
        {
            foreach (var i in Enumerable.Range(0, 10))
            {
                var message = new Message10Contract
                {
                    MyMessageCount = i
                };

                await messaging.SendMessageAsync(message);
                await Task.Delay(10);
            }
        }
    }
}
