using QsMessaging.Public;
using TestContract.MessageContract;

namespace ArrangeInstance01.MessageScenario
{
    internal class Message100Scenarion(IQsMessaging messaging) : IScenario
    {
        public bool IsRepeatable => false;

        public async Task Run()
        {
            var tasks = Enumerable.Range(0, 100)
                .Select(i =>
                {
                    var message = new Message100Contract
                    {
                        MyMessageCount = i
                    };

                    return messaging.SendMessageAsync(message);
                });

            await Task.WhenAll(tasks);
        }
}
}
