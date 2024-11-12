using Contract.MessagesEventsInstance;
using QsMessaging.Public.Handler;

namespace MessagesEventsInstance2.Handler
{
    internal class RegularMessageContractHandler : IQsMessageHandler<RegularMessageContract>
    {
        public Task<bool> Consumer(RegularMessageContract contractModel)
        {
            Console.WriteLine("RegularMessageContractHandler");
            Console.WriteLine(contractModel.MyTextMessage);
            return Task.FromResult(true);
        }
    }
    
}
