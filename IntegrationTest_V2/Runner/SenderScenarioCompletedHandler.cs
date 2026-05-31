using IntegrationTestV2.Contracts;
using QsMessaging.Public.Handler;

namespace IntegrationTestV2.Runner;

public sealed class SenderScenarioCompletedHandler(SenderResultInbox inbox)
    : IQsMessageHandler<SenderScenarioCompletedMessage>
{
    public Task Consumer(SenderScenarioCompletedMessage result)
    {
        inbox.Complete(result);
        return Task.CompletedTask;
    }
}
