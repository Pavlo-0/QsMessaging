using QsMessaging.Public.Handler;
using TestContract.TransportCleanup;

namespace CleanupArrangeInstance01.Handlers;

internal sealed class CleanUpTransportRequestHandler : IQsRequestResponseHandler<CleanUpTransportRequestContract, CleanUpTransportResponseContract>
{
    public Task<CleanUpTransportResponseContract> Consumer(CleanUpTransportRequestContract request)
    {
        return Task.FromResult(new CleanUpTransportResponseContract(request.Id, "cleaned"));
    }
}
