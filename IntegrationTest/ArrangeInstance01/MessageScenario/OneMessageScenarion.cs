using QsMessaging.Public;
using TestContract.MessageContract;

namespace ArrangeInstance01.MessageScenario
{
    internal class OneMessageScenarion(IQsMessaging messaging): IScenario
    {
        public async Task Run()
        {
            var message = new MessageContract
            {
                MyMessageCount = 0
            };

            await messaging.SendMessageAsync(message);
        }
    }
}
