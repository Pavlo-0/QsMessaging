using QsMessaging.Public.Handler;
using TestContract.TransportCleanup;

namespace CleanupArrangeInstance01.Handlers;

internal sealed class FullCleanUpTransportRequestHandler : IQsRequestResponseHandler<FullCleanUpTransportRequestContract, FullCleanUpTransportResponseContract>
{
    public Task<FullCleanUpTransportResponseContract> Consumer(FullCleanUpTransportRequestContract request)
    {
        return Task.FromResult(new FullCleanUpTransportResponseContract(request.Id, "full-cleaned"));
    }
}
