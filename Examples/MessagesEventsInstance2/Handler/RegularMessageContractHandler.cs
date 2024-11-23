﻿using Contract.MessagesEventsInstance;
using QsMessaging.Public.Handler;

namespace MessagesEventsInstance2.Handler
{
    internal class RegularMessageContractHandler : IQsMessageHandler<RegularMessageContract>
    {
        public Task Consumer(RegularMessageContract contractModel)
        {
            Console.WriteLine("Message: RegularMessageContractHandler");
            Console.WriteLine(contractModel.MyTextMessage);

            return Task.CompletedTask;
        }
    }

}
