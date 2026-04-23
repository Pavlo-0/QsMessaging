using QsMessaging.Public.Handler;
using TestContract.TransportCleanup;

namespace CleanupArrangeInstance01.Handlers;

internal sealed class FullCleanUpTransportMessageHandler : IQsMessageHandler<FullCleanUpTransportMessageContract>
{
    public Task Consumer(FullCleanUpTransportMessageContract contractModel)
    {
        return Task.CompletedTask;
    }
}
