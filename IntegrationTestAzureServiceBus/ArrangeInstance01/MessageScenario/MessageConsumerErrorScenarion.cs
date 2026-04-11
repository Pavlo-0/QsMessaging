using QsMessaging.Public;
using TestContract.MessageContract;

namespace ArrangeInstance01.MessageScenario
{
    internal class MessageConsumerErrorScenarion(IQsMessaging messaging): IScenario
    {
        public bool IsRepeatable => false;

        public async Task Run()
        {
            var message = new MessageConsumerErrorContract
            {
                MyMessageCount = 0
            };

            await messaging.SendMessageAsync(message);
        }
    }
}
