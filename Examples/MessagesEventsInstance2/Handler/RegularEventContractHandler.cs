using Contract.MessagesEventsInstance;
using MessagesEventsInstance2.Service;
using QsMessaging.Public.Handler;

namespace MessagesEventsInstance2.Handler
{
    internal class RegularEventContractHandler(ITestService testService) : IQsEventHandler<RegularEventContract>
    {
       
        public Task Consumer(RegularEventContract contractModel)
        {
            Console.WriteLine("Event: RegularEventContractHandler");
            Console.WriteLine(contractModel.MyTextEvent);
            testService.Test();
            return Task.CompletedTask;
        }
    }

}
