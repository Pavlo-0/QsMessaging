﻿using QsMessaging.Public;
using QsMessaging.Public.Handler;
using System.Collections.Concurrent;
using TestContract.MessageContract;

namespace AssertInstance01.MessageAssert
{
    internal class Message50PausedHandler(IQsMessagingConnectionManager connectionManager) : IQsMessageHandler<Message50PausedContract>
    {
        private readonly static ConcurrentBag<Message50PausedContract> _contracts = new ConcurrentBag<Message50PausedContract>();

        public async Task Consumer(Message50PausedContract contractModel)
        {
            _contracts.Add(contractModel);

            if (contractModel.MyMessageCount == 30)
            {
                await connectionManager.Close();
                await Task.Delay(1000);
                await connectionManager.Open();
            }
            

            if (_contracts.Count == 50)
            {
                var i = 0;
                var isFail = false;
                foreach (var contract in _contracts.OrderBy(v=> v.MyMessageCount))
                {
                    if (contract.MyMessageCount != i)
                    {
                        isFail = true;
                    }
                    i++;
                }

                if (isFail)
                {
                    CollectionTestResults.FailTest(TestScenariousEnum.Message50Paused);
                }
                else
                {
                    CollectionTestResults.PassTest(TestScenariousEnum.Message50Paused);
                }
            }
        }
    }
}
