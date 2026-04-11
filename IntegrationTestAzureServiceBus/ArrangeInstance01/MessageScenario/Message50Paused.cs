using QsMessaging.Public;
using TestContract.MessageContract;

namespace ArrangeInstance01.MessageScenario
{
    internal class Message50Paused(IQsMessaging messaging) : IScenario
    {
        public bool IsRepeatable => false;

        public async Task Run()
        {
            foreach (var i in Enumerable.Range(0, 50))
            {
                var message = new Message50PausedContract
                {
                    MyMessageCount = i
                };

                await messaging.SendMessageAsync(message);

                await Task.Delay(100);
            }
        }
    }
}
