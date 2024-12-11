using Contract.MessagesEventsInstance;
using QsMessaging.Public.Handler;

namespace MessagesEventsInstance2.Handler
{
    internal class RegularMessageContract2Handler : IQsMessageHandler<RegularMessageContract2>
    {
        public Task Consumer(RegularMessageContract2 contractModel)
        {
            Console.WriteLine("Record Message: RegularMessageContract2Handler");
            Console.WriteLine(contractModel.MyTextMessage);
            return Task.CompletedTask;
        }
    }
}
