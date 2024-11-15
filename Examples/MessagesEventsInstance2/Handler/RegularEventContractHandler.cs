using Contract.MessagesEventsInstance;
using QsMessaging.Public.Handler;

namespace MessagesEventsInstance2.Handler
{
    internal class RegularEventContractHandler : IQsEventHandler<RegularEventContract>
    {
        public Task<bool> Consumer(RegularEventContract contractModel)
        {
            Console.WriteLine("Event: RegularEventContractHandler");
            Console.WriteLine(contractModel.MyTextEvent);
            return Task.FromResult(true);
        }
    }

}
