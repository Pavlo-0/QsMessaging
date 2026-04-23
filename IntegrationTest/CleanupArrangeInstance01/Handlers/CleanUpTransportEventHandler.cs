using QsMessaging.Public.Handler;
using TestContract.TransportCleanup;

namespace CleanupArrangeInstance01.Handlers;

internal sealed class CleanUpTransportEventHandler : IQsEventHandler<CleanUpTransportEventContract>
{
    public Task Consumer(CleanUpTransportEventContract contract)
    {
        return Task.CompletedTask;
    }
}
