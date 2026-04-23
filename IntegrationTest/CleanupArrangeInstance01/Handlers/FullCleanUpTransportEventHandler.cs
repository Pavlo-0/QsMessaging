using QsMessaging.Public.Handler;
using TestContract.TransportCleanup;

namespace CleanupArrangeInstance01.Handlers;

internal sealed class FullCleanUpTransportEventHandler : IQsEventHandler<FullCleanUpTransportEventContract>
{
    public Task Consumer(FullCleanUpTransportEventContract contract)
    {
        return Task.CompletedTask;
    }
}
