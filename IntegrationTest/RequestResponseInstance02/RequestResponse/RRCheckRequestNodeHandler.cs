using QsMessaging.Public.Handler;
using TestContract.RequestResponse;

namespace RequestResponseInstance02.RequestResponse
{
    internal class RRCheckRequestNodeHandler : IQsRequestResponseHandler<RRRequest2InstanceRequestContract, RRresponse2InstanceRequestContract>
    {
        public Task<RRresponse2InstanceRequestContract> Consumer(RRRequest2InstanceRequestContract request)
        {
            return Task.FromResult(new RRresponse2InstanceRequestContract(request.Id, request.SomeMessage));
        }
    }
}
