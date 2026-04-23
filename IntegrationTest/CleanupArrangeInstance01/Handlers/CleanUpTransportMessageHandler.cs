using QsMessaging.Public.Handler;
using TestContract.TransportCleanup;

namespace CleanupArrangeInstance01.Handlers;

internal sealed class CleanUpTransportMessageHandler : IQsMessageHandler<CleanUpTransportMessageContract>
{
    public Task Consumer(CleanUpTransportMessageContract contractModel)
    {
        return Task.CompletedTask;
    }
}
